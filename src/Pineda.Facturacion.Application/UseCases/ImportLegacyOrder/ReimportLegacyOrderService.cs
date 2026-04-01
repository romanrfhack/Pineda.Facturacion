using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public sealed class ReimportLegacyOrderService
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";
    private const string DefaultTaxObjectCode = "02";

    private readonly PreviewLegacyOrderImportService _previewService;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly IContentHashGenerator _contentHashGenerator;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReimportLegacyOrderService(
        PreviewLegacyOrderImportService previewService,
        ILegacyOrderReader legacyOrderReader,
        IContentHashGenerator contentHashGenerator,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        LegacyImportRevisionRecorder legacyImportRevisionRecorder,
        ISalesOrderRepository salesOrderRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _previewService = previewService;
        _legacyOrderReader = legacyOrderReader;
        _contentHashGenerator = contentHashGenerator;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;
        _salesOrderRepository = salesOrderRepository;
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReimportLegacyOrderResult> ExecuteAsync(ReimportLegacyOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.LegacyOrderId))
        {
            return ValidationFailure(string.Empty, "Legacy order id is required.");
        }

        if (!string.Equals(command.ConfirmationMode, ReimportLegacyOrderResult.ReplaceExistingImportConfirmationMode, StringComparison.Ordinal))
        {
            return ValidationFailure(command.LegacyOrderId, "Explicit confirmation mode is required to replace the existing import.");
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedExistingSourceHash) || string.IsNullOrWhiteSpace(command.ExpectedCurrentSourceHash))
        {
            return ValidationFailure(command.LegacyOrderId, "Expected source hashes are required to execute controlled reimport.");
        }

        var preview = await _previewService.ExecuteAsync(command.LegacyOrderId, cancellationToken);
        if (!preview.IsSuccess)
        {
            return new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.NotFound,
                IsSuccess = false,
                LegacyOrderId = command.LegacyOrderId,
                ErrorMessage = preview.ErrorMessage ?? $"Legacy order '{command.LegacyOrderId}' was not found."
            };
        }

        if (!string.Equals(preview.ExistingSourceHash, command.ExpectedExistingSourceHash, StringComparison.Ordinal)
            || !string.Equals(preview.CurrentSourceHash, command.ExpectedCurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Reimport preview is no longer current. Refresh the preview and confirm the new hashes before retrying.");
        }

        if (preview.ReimportEligibility.Status != PreviewLegacyOrderReimportEligibilityStatus.Allowed)
        {
            return EligibilityConflict(preview);
        }

        var currentLegacyOrder = await _legacyOrderReader.GetByIdAsync(command.LegacyOrderId, cancellationToken);
        if (currentLegacyOrder is null)
        {
            return new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.NotFound,
                IsSuccess = false,
                LegacyOrderId = command.LegacyOrderId,
                ErrorMessage = $"Legacy order '{command.LegacyOrderId}' was not found."
            };
        }

        var applyHash = _contentHashGenerator.GenerateHash(currentLegacyOrder);
        if (!string.Equals(applyHash, preview.CurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Legacy source data changed after the preview. Refresh the preview before retrying reimport.");
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(
            LegacySourceSystem,
            LegacyOrdersSourceTable,
            command.LegacyOrderId,
            cancellationToken);

        if (importRecord is null)
        {
            return new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.NotFound,
                IsSuccess = false,
                LegacyOrderId = command.LegacyOrderId,
                ErrorMessage = $"Legacy order '{command.LegacyOrderId}' has not been imported yet."
            };
        }

        if (!string.Equals(importRecord.SourceHash, preview.ExistingSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Existing import state changed after the preview. Refresh the preview before retrying reimport.");
        }

        var salesOrder = preview.ExistingSalesOrderId.HasValue
            ? await _salesOrderRepository.GetTrackedByIdWithItemsAsync(preview.ExistingSalesOrderId.Value, cancellationToken)
            : null;

        if (salesOrder is null)
        {
            return new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.NotFound,
                IsSuccess = false,
                LegacyOrderId = command.LegacyOrderId,
                LegacyImportRecordId = importRecord.Id,
                ErrorMessage = $"Sales order snapshot for legacy order '{command.LegacyOrderId}' was not found."
            };
        }

        try
        {
            var replacementSnapshot = LegacyOrderSnapshotMapper.MapToSalesOrder(currentLegacyOrder, salesOrder.LegacyImportRecordId);
            ApplySalesOrderReplacement(salesOrder, replacementSnapshot);

            BillingDocument? billingDocument = null;
            if (preview.ExistingBillingDocumentId.HasValue)
            {
                billingDocument = await _billingDocumentRepository.GetTrackedByIdAsync(preview.ExistingBillingDocumentId.Value, cancellationToken);
                if (billingDocument is null)
                {
                    return new ReimportLegacyOrderResult
                    {
                        Outcome = ReimportLegacyOrderOutcome.NotFound,
                        IsSuccess = false,
                        LegacyOrderId = command.LegacyOrderId,
                        LegacyImportRecordId = importRecord.Id,
                        SalesOrderId = salesOrder.Id,
                        ErrorMessage = $"Billing document '{preview.ExistingBillingDocumentId.Value}' was not found."
                    };
                }

                if (!LegacyOrderReimportPolicy.CanEditBillingDocument(billingDocument.Status.ToString()))
                {
                    return ProtectedStateConflict(preview, $"Reimport is blocked because billing document '{billingDocument.Id}' is in protected state '{billingDocument.Status}'.");
                }

                var sourceLegacyOrderId = BuildLegacyOrderReference(salesOrder, importRecord);
                ApplyBillingDocumentReplacement(billingDocument, salesOrder, sourceLegacyOrderId);
            }

            FiscalDocument? fiscalDocument = null;
            if (preview.ExistingFiscalDocumentId.HasValue)
            {
                if (billingDocument is null)
                {
                    return ProtectedStateConflict(preview, "Reimport is blocked because the existing fiscal document is not linked to an editable billing document.");
                }

                fiscalDocument = await _fiscalDocumentRepository.GetTrackedByBillingDocumentIdAsync(billingDocument.Id, cancellationToken);
                if (fiscalDocument is null)
                {
                    return new ReimportLegacyOrderResult
                    {
                        Outcome = ReimportLegacyOrderOutcome.NotFound,
                        IsSuccess = false,
                        LegacyOrderId = command.LegacyOrderId,
                        LegacyImportRecordId = importRecord.Id,
                        SalesOrderId = salesOrder.Id,
                        BillingDocumentId = billingDocument.Id,
                        ErrorMessage = $"Fiscal document '{preview.ExistingFiscalDocumentId.Value}' was not found."
                    };
                }

                if (!LegacyOrderReimportPolicy.CanEditFiscalComposition(fiscalDocument.Status.ToString()))
                {
                    return string.Equals(fiscalDocument.Status.ToString(), nameof(FiscalDocumentStatus.Stamped), StringComparison.Ordinal)
                        ? StampedConflict(preview)
                        : ProtectedStateConflict(preview, $"Reimport is blocked because fiscal document '{fiscalDocument.Id}' is in protected state '{fiscalDocument.Status}'.");
                }

                var fiscalItems = await BuildFiscalItemsAsync(billingDocument.Items, fiscalDocument.Id, cancellationToken);
                ApplyFiscalDocumentReplacement(fiscalDocument, billingDocument, fiscalItems);
            }

            var previousSourceHash = importRecord.SourceHash;
            var now = DateTime.UtcNow;
            importRecord.SourceHash = applyHash;
            importRecord.ImportStatus = ImportStatus.Imported;
            importRecord.ImportedAtUtc = now;
            importRecord.LastSeenAtUtc = now;
            importRecord.ErrorMessage = null;
            importRecord.BillingDocumentId = billingDocument?.Id ?? importRecord.BillingDocumentId;
            await _legacyImportRecordRepository.UpdateAsync(importRecord, cancellationToken);

            var revisionDraft = new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.Reimported,
                IsSuccess = true,
                LegacyOrderId = command.LegacyOrderId,
                LegacyImportRecordId = importRecord.Id,
                SalesOrderId = salesOrder.Id,
                SalesOrderStatus = salesOrder.Status.ToString(),
                BillingDocumentId = billingDocument?.Id,
                BillingDocumentStatus = billingDocument?.Status.ToString(),
                FiscalDocumentId = fiscalDocument?.Id,
                FiscalDocumentStatus = fiscalDocument?.Status.ToString(),
                PreviousSourceHash = previousSourceHash,
                NewSourceHash = applyHash,
                ReimportApplied = true,
                ReimportMode = command.ConfirmationMode,
                ReimportEligibility = preview.ReimportEligibility,
                AllowedActions = LegacyOrderReimportPolicy.BuildAllowedActions(new ImportedLegacyOrderLookupModel
                {
                    LegacyOrderId = command.LegacyOrderId,
                    SalesOrderId = salesOrder.Id,
                    SalesOrderStatus = salesOrder.Status.ToString(),
                    BillingDocumentId = billingDocument?.Id,
                    BillingDocumentStatus = billingDocument?.Status.ToString(),
                    FiscalDocumentId = fiscalDocument?.Id,
                    FiscalDocumentStatus = fiscalDocument?.Status.ToString(),
                    FiscalUuid = preview.FiscalUuid,
                    ExistingSourceHash = applyHash
                })
            };

            var currentRevisionNumber = await _legacyImportRevisionRecorder.RecordReimportedAsync(
                importRecord,
                preview,
                revisionDraft,
                currentLegacyOrder,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            revisionDraft.CurrentRevisionNumber = currentRevisionNumber;
            return revisionDraft;
        }
        catch (InvalidOperationException exception)
        {
            return ProtectedStateConflict(preview, exception.Message);
        }
    }

    private static ReimportLegacyOrderResult ValidationFailure(string legacyOrderId, string errorMessage)
    {
        return new ReimportLegacyOrderResult
        {
            Outcome = ReimportLegacyOrderOutcome.ValidationFailed,
            IsSuccess = false,
            LegacyOrderId = legacyOrderId,
            ErrorCode = ReimportLegacyOrderResult.ConfirmationRequiredErrorCode,
            ErrorMessage = errorMessage
        };
    }

    private static ReimportLegacyOrderResult PreviewExpiredConflict(PreviewLegacyOrderImportResult preview, string errorMessage)
    {
        return new ReimportLegacyOrderResult
        {
            Outcome = ReimportLegacyOrderOutcome.Conflict,
            IsSuccess = false,
            LegacyOrderId = preview.LegacyOrderId,
            LegacyImportRecordId = null,
            SalesOrderId = preview.ExistingSalesOrderId,
            SalesOrderStatus = preview.ExistingSalesOrderStatus,
            BillingDocumentId = preview.ExistingBillingDocumentId,
            BillingDocumentStatus = preview.ExistingBillingDocumentStatus,
            FiscalDocumentId = preview.ExistingFiscalDocumentId,
            FiscalDocumentStatus = preview.ExistingFiscalDocumentStatus,
            FiscalUuid = preview.FiscalUuid,
            PreviousSourceHash = preview.ExistingSourceHash,
            NewSourceHash = preview.CurrentSourceHash,
            ErrorCode = ReimportLegacyOrderResult.PreviewExpiredErrorCode,
            ErrorMessage = errorMessage,
            ReimportEligibility = preview.ReimportEligibility,
            AllowedActions = preview.AllowedActions
        };
    }

    private static ReimportLegacyOrderResult EligibilityConflict(PreviewLegacyOrderImportResult preview)
    {
        return preview.ReimportEligibility.Status switch
        {
            PreviewLegacyOrderReimportEligibilityStatus.BlockedByStampedFiscalDocument => StampedConflict(preview),
            PreviewLegacyOrderReimportEligibilityStatus.NotNeededNoChanges => new ReimportLegacyOrderResult
            {
                Outcome = ReimportLegacyOrderOutcome.Conflict,
                IsSuccess = false,
                LegacyOrderId = preview.LegacyOrderId,
                SalesOrderId = preview.ExistingSalesOrderId,
                SalesOrderStatus = preview.ExistingSalesOrderStatus,
                BillingDocumentId = preview.ExistingBillingDocumentId,
                BillingDocumentStatus = preview.ExistingBillingDocumentStatus,
                FiscalDocumentId = preview.ExistingFiscalDocumentId,
                FiscalDocumentStatus = preview.ExistingFiscalDocumentStatus,
                FiscalUuid = preview.FiscalUuid,
                PreviousSourceHash = preview.ExistingSourceHash,
                NewSourceHash = preview.CurrentSourceHash,
                ErrorCode = ReimportLegacyOrderResult.ReimportNoChangesDetectedErrorCode,
                ErrorMessage = preview.ReimportEligibility.ReasonMessage,
                ReimportEligibility = preview.ReimportEligibility,
                AllowedActions = preview.AllowedActions
            },
            _ => ProtectedStateConflict(preview, preview.ReimportEligibility.ReasonMessage)
        };
    }

    private static ReimportLegacyOrderResult StampedConflict(PreviewLegacyOrderImportResult preview)
    {
        return new ReimportLegacyOrderResult
        {
            Outcome = ReimportLegacyOrderOutcome.Conflict,
            IsSuccess = false,
            LegacyOrderId = preview.LegacyOrderId,
            SalesOrderId = preview.ExistingSalesOrderId,
            SalesOrderStatus = preview.ExistingSalesOrderStatus,
            BillingDocumentId = preview.ExistingBillingDocumentId,
            BillingDocumentStatus = preview.ExistingBillingDocumentStatus,
            FiscalDocumentId = preview.ExistingFiscalDocumentId,
            FiscalDocumentStatus = preview.ExistingFiscalDocumentStatus,
            FiscalUuid = preview.FiscalUuid,
            PreviousSourceHash = preview.ExistingSourceHash,
            NewSourceHash = preview.CurrentSourceHash,
            ErrorCode = ReimportLegacyOrderResult.ReimportBlockedByStampedFiscalDocumentErrorCode,
            ErrorMessage = preview.ReimportEligibility.ReasonMessage,
            ReimportEligibility = preview.ReimportEligibility,
            AllowedActions = preview.AllowedActions
        };
    }

    private static ReimportLegacyOrderResult ProtectedStateConflict(PreviewLegacyOrderImportResult preview, string errorMessage)
    {
        return new ReimportLegacyOrderResult
        {
            Outcome = ReimportLegacyOrderOutcome.Conflict,
            IsSuccess = false,
            LegacyOrderId = preview.LegacyOrderId,
            SalesOrderId = preview.ExistingSalesOrderId,
            SalesOrderStatus = preview.ExistingSalesOrderStatus,
            BillingDocumentId = preview.ExistingBillingDocumentId,
            BillingDocumentStatus = preview.ExistingBillingDocumentStatus,
            FiscalDocumentId = preview.ExistingFiscalDocumentId,
            FiscalDocumentStatus = preview.ExistingFiscalDocumentStatus,
            FiscalUuid = preview.FiscalUuid,
            PreviousSourceHash = preview.ExistingSourceHash,
            NewSourceHash = preview.CurrentSourceHash,
            ErrorCode = ReimportLegacyOrderResult.ReimportBlockedByProtectedStateErrorCode,
            ErrorMessage = errorMessage,
            ReimportEligibility = preview.ReimportEligibility,
            AllowedActions = preview.AllowedActions
        };
    }

    private static void ApplySalesOrderReplacement(SalesOrder salesOrder, SalesOrder replacementSnapshot)
    {
        salesOrder.LegacyOrderNumber = replacementSnapshot.LegacyOrderNumber;
        salesOrder.LegacyOrderType = replacementSnapshot.LegacyOrderType;
        salesOrder.CustomerLegacyId = replacementSnapshot.CustomerLegacyId;
        salesOrder.CustomerName = replacementSnapshot.CustomerName;
        salesOrder.CustomerRfc = replacementSnapshot.CustomerRfc;
        salesOrder.PaymentCondition = replacementSnapshot.PaymentCondition;
        salesOrder.PriceListCode = replacementSnapshot.PriceListCode;
        salesOrder.DeliveryType = replacementSnapshot.DeliveryType;
        salesOrder.CurrencyCode = replacementSnapshot.CurrencyCode;
        salesOrder.SnapshotTakenAtUtc = DateTime.UtcNow;
        SyncCollection(
            salesOrder.Items,
            replacementSnapshot.Items.OrderBy(x => x.LineNumber).ToList(),
            () => new SalesOrderItem(),
            static (current, next) =>
            {
                current.LineNumber = next.LineNumber;
                current.LegacyArticleId = next.LegacyArticleId;
                current.Sku = next.Sku;
                current.Description = next.Description;
                current.UnitCode = next.UnitCode;
                current.UnitName = next.UnitName;
                current.Quantity = next.Quantity;
                current.UnitPrice = next.UnitPrice;
                current.DiscountAmount = next.DiscountAmount;
                current.TaxRate = next.TaxRate;
                current.TaxAmount = next.TaxAmount;
                current.LineTotal = next.LineTotal;
                current.SatProductServiceCode = next.SatProductServiceCode;
                current.SatUnitCode = next.SatUnitCode;
            });
        StandardVat16Calculator.RecalculateTotals(salesOrder);
    }

    private static void ApplyBillingDocumentReplacement(BillingDocument billingDocument, SalesOrder salesOrder, string sourceLegacyOrderId)
    {
        billingDocument.SalesOrderId = salesOrder.Id;
        billingDocument.PaymentCondition = salesOrder.PaymentCondition;
        billingDocument.CurrencyCode = salesOrder.CurrencyCode;
        billingDocument.ExchangeRate = salesOrder.CurrencyCode == "MXN" ? 1m : billingDocument.ExchangeRate;
        var nextItems = salesOrder.Items
            .OrderBy(x => x.LineNumber)
            .Select(item => new BillingDocumentItem
            {
                SalesOrderId = salesOrder.Id,
                SalesOrderItemId = item.Id,
                SourceSalesOrderLineNumber = item.LineNumber,
                SourceLegacyOrderId = sourceLegacyOrderId,
                LineNumber = item.LineNumber,
                Sku = item.Sku,
                ProductInternalCode = string.IsNullOrWhiteSpace(item.Sku) ? null : item.Sku.Trim().ToUpperInvariant(),
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxRate = StandardVat16Calculator.StandardVatRate,
                TaxAmount = 0m,
                LineTotal = 0m,
                SatProductServiceCode = item.SatProductServiceCode,
                SatUnitCode = item.SatUnitCode,
                TaxObjectCode = DefaultTaxObjectCode
            })
            .ToList();

        SyncCollection(
            billingDocument.Items,
            nextItems,
            () => new BillingDocumentItem(),
            static (current, next) =>
            {
                current.SalesOrderId = next.SalesOrderId;
                current.SalesOrderItemId = next.SalesOrderItemId;
                current.SourceBillingDocumentItemRemovalId = next.SourceBillingDocumentItemRemovalId;
                current.SourceSalesOrderLineNumber = next.SourceSalesOrderLineNumber;
                current.SourceLegacyOrderId = next.SourceLegacyOrderId;
                current.LineNumber = next.LineNumber;
                current.Sku = next.Sku;
                current.ProductInternalCode = next.ProductInternalCode;
                current.Description = next.Description;
                current.Quantity = next.Quantity;
                current.UnitPrice = next.UnitPrice;
                current.DiscountAmount = next.DiscountAmount;
                current.TaxRate = next.TaxRate;
                current.TaxAmount = next.TaxAmount;
                current.LineTotal = next.LineTotal;
                current.SatProductServiceCode = next.SatProductServiceCode;
                current.SatUnitCode = next.SatUnitCode;
                current.TaxObjectCode = next.TaxObjectCode;
            });
        StandardVat16Calculator.ApplyStandardVat(billingDocument);
        billingDocument.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<List<FiscalDocumentItem>> BuildFiscalItemsAsync(
        IReadOnlyList<BillingDocumentItem> billingDocumentItems,
        long fiscalDocumentId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fiscalItems = new List<FiscalDocumentItem>(billingDocumentItems.Count);

        foreach (var billingDocumentItem in billingDocumentItems.OrderBy(x => x.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(billingDocumentItem.ProductInternalCode))
            {
                throw new InvalidOperationException(
                    $"Billing document item line '{billingDocumentItem.LineNumber}' does not contain the persisted product internal code required for fiscal resolution.");
            }

            var internalCode = billingDocumentItem.ProductInternalCode.Trim().ToUpperInvariant();
            var productFiscalProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(internalCode, cancellationToken);
            if (productFiscalProfile is null || !productFiscalProfile.IsActive)
            {
                throw new InvalidOperationException(
                    $"No active product fiscal profile exists for item line '{billingDocumentItem.LineNumber}' and internal code '{internalCode}'.");
            }

            fiscalItems.Add(new FiscalDocumentItem
            {
                FiscalDocumentId = fiscalDocumentId,
                BillingDocumentItemId = billingDocumentItem.Id > 0 ? billingDocumentItem.Id : null,
                LineNumber = billingDocumentItem.LineNumber,
                InternalCode = productFiscalProfile.InternalCode,
                Description = billingDocumentItem.Description,
                Quantity = billingDocumentItem.Quantity,
                UnitPrice = billingDocumentItem.UnitPrice,
                DiscountAmount = billingDocumentItem.DiscountAmount,
                Subtotal = billingDocumentItem.LineTotal,
                TaxTotal = billingDocumentItem.TaxAmount,
                Total = billingDocumentItem.LineTotal + billingDocumentItem.TaxAmount,
                SatProductServiceCode = productFiscalProfile.SatProductServiceCode,
                SatUnitCode = productFiscalProfile.SatUnitCode,
                TaxObjectCode = productFiscalProfile.TaxObjectCode,
                VatRate = productFiscalProfile.VatRate,
                UnitText = productFiscalProfile.DefaultUnitText,
                CreatedAtUtc = now
            });
        }

        return fiscalItems;
    }

    private static void ApplyFiscalDocumentReplacement(
        FiscalDocument fiscalDocument,
        BillingDocument billingDocument,
        IReadOnlyList<FiscalDocumentItem> fiscalItems)
    {
        SyncCollection(
            fiscalDocument.Items,
            fiscalItems.OrderBy(x => x.LineNumber).ToList(),
            () => new FiscalDocumentItem(),
            static (current, next) =>
            {
                current.BillingDocumentItemId = next.BillingDocumentItemId;
                current.LineNumber = next.LineNumber;
                current.InternalCode = next.InternalCode;
                current.Description = next.Description;
                current.Quantity = next.Quantity;
                current.UnitPrice = next.UnitPrice;
                current.DiscountAmount = next.DiscountAmount;
                current.Subtotal = next.Subtotal;
                current.TaxTotal = next.TaxTotal;
                current.Total = next.Total;
                current.SatProductServiceCode = next.SatProductServiceCode;
                current.SatUnitCode = next.SatUnitCode;
                current.TaxObjectCode = next.TaxObjectCode;
                current.VatRate = next.VatRate;
                current.UnitText = next.UnitText;
                current.CreatedAtUtc = next.CreatedAtUtc;
            });
        fiscalDocument.Subtotal = billingDocument.Subtotal;
        fiscalDocument.DiscountTotal = billingDocument.DiscountTotal;
        fiscalDocument.TaxTotal = billingDocument.TaxTotal;
        fiscalDocument.Total = billingDocument.Total;
        fiscalDocument.UpdatedAtUtc = DateTime.UtcNow;
        fiscalDocument.Status = FiscalDocumentStatus.ReadyForStamping;
    }

    private static string BuildLegacyOrderReference(SalesOrder salesOrder, LegacyImportRecord legacyImportRecord)
    {
        return string.IsNullOrWhiteSpace(legacyImportRecord.SourceDocumentId)
            ? salesOrder.LegacyOrderNumber
            : string.IsNullOrWhiteSpace(salesOrder.LegacyOrderNumber)
                ? legacyImportRecord.SourceDocumentId
                : $"{legacyImportRecord.SourceDocumentId}-{salesOrder.LegacyOrderNumber}";
    }

    private static void SyncCollection<T>(
        List<T> currentItems,
        IReadOnlyList<T> nextItems,
        Func<T> factory,
        Action<T, T> apply)
        where T : class
    {
        var overlap = Math.Min(currentItems.Count, nextItems.Count);

        for (var index = 0; index < overlap; index++)
        {
            apply(currentItems[index], nextItems[index]);
        }

        while (currentItems.Count > nextItems.Count)
        {
            currentItems.RemoveAt(currentItems.Count - 1);
        }

        for (var index = overlap; index < nextItems.Count; index++)
        {
            var item = factory();
            apply(item, nextItems[index]);
            currentItems.Add(item);
        }
    }
}
