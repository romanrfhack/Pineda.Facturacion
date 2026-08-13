using Microsoft.Extensions.Logging;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class GetBillingDocumentLegacyPaymentSuggestionService
{
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderPaymentReader _legacyOrderPaymentReader;
    private readonly ILogger<GetBillingDocumentLegacyPaymentSuggestionService> _logger;

    public GetBillingDocumentLegacyPaymentSuggestionService(
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        ILegacyOrderPaymentReader legacyOrderPaymentReader,
        ILogger<GetBillingDocumentLegacyPaymentSuggestionService> logger)
    {
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _legacyOrderPaymentReader = legacyOrderPaymentReader;
        _logger = logger;
    }

    public async Task<BillingDocumentLegacyPaymentSuggestionResult> ExecuteAsync(
        long billingDocumentId,
        CancellationToken cancellationToken = default)
    {
        if (billingDocumentId <= 0)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(0);
        }

        var salesOrders = await _salesOrderSnapshotRepository.GetByBillingDocumentIdWithItemsAsync(
            billingDocumentId,
            cancellationToken);
        if (salesOrders.Count == 0)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(0);
        }

        var paymentCodes = new List<string?>(salesOrders.Count);
        foreach (var salesOrder in salesOrders)
        {
            var paymentCode = salesOrder.LegacyPaymentCode;
            if (string.IsNullOrWhiteSpace(paymentCode))
            {
                var importRecord = await _legacyImportRecordRepository.GetByIdAsync(
                    salesOrder.LegacyImportRecordId,
                    cancellationToken);
                if (importRecord is not null && !string.IsNullOrWhiteSpace(importRecord.SourceDocumentId))
                {
                    try
                    {
                        var livePayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                            importRecord.SourceDocumentId,
                            cancellationToken);
                        paymentCode = livePayment?.LegacyPaymentCode;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Legacy payment suggestion lookup failed for BillingDocumentId={BillingDocumentId} LegacyOrderId={LegacyOrderId}. Manual fiscal capture remains available.",
                            billingDocumentId,
                            importRecord.SourceDocumentId);
                        return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(salesOrders.Count);
                    }
                }
            }

            paymentCodes.Add(paymentCode);
        }

        var individualSuggestions = paymentCodes
            .Select(LegacyPaymentSuggestionPolicy.Resolve)
            .ToArray();
        if (individualSuggestions.Any(x => x is null))
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unknown(salesOrders.Count);
        }

        var commonSuggestion = LegacyPaymentSuggestionPolicy.ResolveCommon(paymentCodes);
        if (commonSuggestion is null)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Mixed(salesOrders.Count);
        }

        return BillingDocumentLegacyPaymentSuggestionResult.Suggested(
            salesOrders.Count,
            commonSuggestion);
    }
}

public static class LegacyPaymentSuggestionStatuses
{
    public const string Suggested = "Suggested";
    public const string Mixed = "Mixed";
    public const string Unknown = "Unknown";
    public const string Unavailable = "Unavailable";
}

public sealed class BillingDocumentLegacyPaymentSuggestionResult
{
    public string Status { get; init; } = LegacyPaymentSuggestionStatuses.Unavailable;

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public bool? IsCreditSale { get; init; }

    public string? SourceDescription { get; init; }

    public int SourceOrderCount { get; init; }

    public static BillingDocumentLegacyPaymentSuggestionResult Suggested(
        int sourceOrderCount,
        LegacyPaymentSuggestion suggestion)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Suggested,
            PaymentMethodSat = suggestion.PaymentMethodSat,
            PaymentFormSat = suggestion.PaymentFormSat,
            IsCreditSale = suggestion.IsCreditSale,
            SourceDescription = suggestion.SourceDescription,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Mixed(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Mixed,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Unknown(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Unknown,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Unavailable(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Unavailable,
            SourceOrderCount = sourceOrderCount
        };
    }
}
