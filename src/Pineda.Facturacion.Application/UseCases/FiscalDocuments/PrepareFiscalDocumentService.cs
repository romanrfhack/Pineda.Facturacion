using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using System.Globalization;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class PrepareFiscalDocumentService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISatCatalogDescriptionProvider _satCatalogDescriptionProvider;
    private readonly IUnitOfWork _unitOfWork;

    public PrepareFiscalDocumentService(
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _satCatalogDescriptionProvider = satCatalogDescriptionProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<PrepareFiscalDocumentResult> ExecuteAsync(
        PrepareFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.BillingDocumentId <= 0)
        {
            return ValidationFailure(command.BillingDocumentId, "Billing document id is required.");
        }

        if (command.FiscalReceiverId <= 0)
        {
            return ValidationFailure(command.BillingDocumentId, "Fiscal receiver id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentMethodSat))
        {
            return ValidationFailure(command.BillingDocumentId, "Payment method SAT is required.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentFormSat))
        {
            return ValidationFailure(command.BillingDocumentId, "Payment form SAT is required.");
        }

        var paymentMethodSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentMethodSat);
        var paymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentFormSat);
        var paymentCondition = FiscalMasterDataNormalization.NormalizeOptionalText(command.PaymentCondition);

        if (string.IsNullOrWhiteSpace(paymentCondition))
        {
            return ValidationFailure(command.BillingDocumentId, "Payment condition is required.");
        }

        if (paymentCondition.Length > 50)
        {
            return ValidationFailure(command.BillingDocumentId, "Payment condition must be 50 characters or fewer.");
        }

        if (!_satCatalogDescriptionProvider.GetPaymentMethods().ContainsKey(paymentMethodSat))
        {
            return ValidationFailure(command.BillingDocumentId, $"Payment method SAT '{paymentMethodSat}' is not valid.");
        }

        if (!_satCatalogDescriptionProvider.GetPaymentForms().ContainsKey(paymentFormSat))
        {
            return ValidationFailure(command.BillingDocumentId, $"Payment form SAT '{paymentFormSat}' is not valid.");
        }

        if (string.Equals(paymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(paymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure(command.BillingDocumentId, "Payment method SAT 'PPD' requires payment form SAT '99'.");
        }

        if (string.Equals(paymentMethodSat, "PUE", StringComparison.OrdinalIgnoreCase)
            && string.Equals(paymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure(command.BillingDocumentId, "Payment method SAT 'PUE' does not allow payment form SAT '99'.");
        }

        if (command.IsCreditSale)
        {
            if (command.CreditDays is null or <= 0)
            {
                return ValidationFailure(command.BillingDocumentId, "Credit days must be greater than zero for credit sales.");
            }

            if (!string.Equals(paymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationFailure(command.BillingDocumentId, "Credit sales require payment method SAT 'PPD'.");
            }

            if (!string.Equals(paymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationFailure(command.BillingDocumentId, "Credit sales require payment form SAT '99'.");
            }
        }

        var billingDocument = await _billingDocumentRepository.GetByIdAsync(command.BillingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return new PrepareFiscalDocumentResult
            {
                Outcome = PrepareFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                BillingDocumentId = command.BillingDocumentId,
                ErrorMessage = $"Billing document '{command.BillingDocumentId}' was not found."
            };
        }

        if (billingDocument.Status != BillingDocumentStatus.Draft)
        {
            return ValidationFailure(command.BillingDocumentId, "Billing document is not eligible for fiscal snapshot creation.");
        }

        var existingFiscalDocument = await _fiscalDocumentRepository.GetByBillingDocumentIdAsync(command.BillingDocumentId, cancellationToken);
        if (existingFiscalDocument is not null)
        {
            return new PrepareFiscalDocumentResult
            {
                Outcome = PrepareFiscalDocumentOutcome.Conflict,
                IsSuccess = false,
                BillingDocumentId = command.BillingDocumentId,
                FiscalDocumentId = existingFiscalDocument.Id,
                Status = existingFiscalDocument.Status,
                ErrorMessage = $"Billing document '{command.BillingDocumentId}' already has a fiscal document."
            };
        }

        var issuerProfile = await ResolveIssuerProfileAsync(command, cancellationToken);
        if (issuerProfile is null || !issuerProfile.IsActive || !HasRequiredIssuerFields(issuerProfile))
        {
            return new PrepareFiscalDocumentResult
            {
                Outcome = PrepareFiscalDocumentOutcome.MissingIssuerProfile,
                IsSuccess = false,
                BillingDocumentId = command.BillingDocumentId,
                ErrorMessage = "An active issuer profile with required fiscal fields is required."
            };
        }

        var fiscalReceiver = await _fiscalReceiverRepository.GetByIdAsync(command.FiscalReceiverId, cancellationToken);
        if (fiscalReceiver is null || !fiscalReceiver.IsActive)
        {
            return new PrepareFiscalDocumentResult
            {
                Outcome = PrepareFiscalDocumentOutcome.MissingReceiver,
                IsSuccess = false,
                BillingDocumentId = command.BillingDocumentId,
                ErrorMessage = $"Fiscal receiver '{command.FiscalReceiverId}' was not found or is inactive."
            };
        }

        var receiverCfdiUseCode = ResolveReceiverCfdiUseCode(command, fiscalReceiver);
        if (!HasRequiredReceiverFields(fiscalReceiver, receiverCfdiUseCode))
        {
            return new PrepareFiscalDocumentResult
            {
                Outcome = PrepareFiscalDocumentOutcome.MissingReceiver,
                IsSuccess = false,
                BillingDocumentId = command.BillingDocumentId,
                ErrorMessage = "Fiscal receiver is missing required fiscal fields."
            };
        }

        var specialFieldValidationError = ValidateSpecialFields(fiscalReceiver, command.SpecialFields);
        if (specialFieldValidationError is not null)
        {
            return ValidationFailure(command.BillingDocumentId, specialFieldValidationError);
        }

        if (string.IsNullOrWhiteSpace(billingDocument.CurrencyCode))
        {
            return ValidationFailure(command.BillingDocumentId, "Billing document currency code is required.");
        }

        var currencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocument.CurrencyCode);
        var exchangeRate = billingDocument.ExchangeRate;

        if (currencyCode != "MXN")
        {
            return ValidationFailure(
                command.BillingDocumentId,
                $"Current MVP fiscal snapshot preparation supports MXN only. Billing document currency '{currencyCode}' is not supported yet.");
        }

        if (currencyCode == "MXN")
        {
            exchangeRate = 1m;
        }

        var now = DateTime.UtcNow;
        var itemResults = new List<FiscalDocumentItem>();

        foreach (var billingDocumentItem in billingDocument.Items.OrderBy(x => x.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(billingDocumentItem.ProductInternalCode))
            {
                return new PrepareFiscalDocumentResult
                {
                    Outcome = PrepareFiscalDocumentOutcome.MissingProductFiscalProfile,
                    IsSuccess = false,
                    BillingDocumentId = command.BillingDocumentId,
                    ErrorMessage = $"Billing document item line '{billingDocumentItem.LineNumber}' does not contain the persisted product internal code required for fiscal resolution."
                };
            }

            var internalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocumentItem.ProductInternalCode);
            var productFiscalProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(internalCode, cancellationToken);
            if (productFiscalProfile is null || !productFiscalProfile.IsActive)
            {
                return new PrepareFiscalDocumentResult
                {
                    Outcome = PrepareFiscalDocumentOutcome.MissingProductFiscalProfile,
                    IsSuccess = false,
                    BillingDocumentId = command.BillingDocumentId,
                    ErrorMessage = $"No active product fiscal profile exists for item line '{billingDocumentItem.LineNumber}' and internal code '{internalCode}'."
                };
            }

            if (billingDocumentItem.TaxRate != productFiscalProfile.VatRate)
            {
                return ValidationFailure(
                    command.BillingDocumentId,
                    $"Billing document item line '{billingDocumentItem.LineNumber}' tax rate '{billingDocumentItem.TaxRate}' does not match product fiscal profile VAT rate '{productFiscalProfile.VatRate}'.");
            }

            itemResults.Add(new FiscalDocumentItem
            {
                BillingDocumentItemId = billingDocumentItem.Id,
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

        var folioAssignment = await ReserveFiscalFolioAsync(issuerProfile, cancellationToken);
        if (folioAssignment.ErrorMessage is not null)
        {
            return ValidationFailure(command.BillingDocumentId, folioAssignment.ErrorMessage);
        }

        var fiscalDocument = new FiscalDocument
        {
            BillingDocumentId = billingDocument.Id,
            IssuerProfileId = issuerProfile.Id,
            FiscalReceiverId = fiscalReceiver.Id,
            Status = FiscalDocumentStatus.ReadyForStamping,
            CfdiVersion = issuerProfile.CfdiVersion,
            DocumentType = billingDocument.DocumentType,
            Series = folioAssignment.Series,
            Folio = folioAssignment.Folio,
            IssuedAtUtc = command.IssuedAtUtc ?? now,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            PaymentMethodSat = paymentMethodSat,
            PaymentFormSat = paymentFormSat,
            PaymentCondition = paymentCondition,
            IsCreditSale = command.IsCreditSale,
            CreditDays = command.IsCreditSale ? command.CreditDays : null,
            IssuerRfc = issuerProfile.Rfc,
            IssuerLegalName = issuerProfile.LegalName,
            IssuerFiscalRegimeCode = issuerProfile.FiscalRegimeCode,
            IssuerPostalCode = issuerProfile.PostalCode,
            PacEnvironment = issuerProfile.PacEnvironment,
            CertificateReference = issuerProfile.CertificateReference,
            PrivateKeyReference = issuerProfile.PrivateKeyReference,
            PrivateKeyPasswordReference = issuerProfile.PrivateKeyPasswordReference,
            ReceiverRfc = fiscalReceiver.Rfc,
            ReceiverLegalName = fiscalReceiver.LegalName,
            ReceiverFiscalRegimeCode = fiscalReceiver.FiscalRegimeCode,
            ReceiverCfdiUseCode = receiverCfdiUseCode!,
            ReceiverPostalCode = fiscalReceiver.PostalCode,
            ReceiverCountryCode = fiscalReceiver.CountryCode,
            ReceiverForeignTaxRegistration = fiscalReceiver.ForeignTaxRegistration,
            Subtotal = billingDocument.Subtotal,
            DiscountTotal = billingDocument.DiscountTotal,
            TaxTotal = billingDocument.TaxTotal,
            Total = billingDocument.Total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items = itemResults,
            SpecialFieldValues = BuildSpecialFieldSnapshots(fiscalReceiver, command.SpecialFields, now)
        };

        await _fiscalDocumentRepository.AddAsync(fiscalDocument, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PrepareFiscalDocumentResult
        {
            Outcome = PrepareFiscalDocumentOutcome.Created,
            IsSuccess = true,
            BillingDocumentId = billingDocument.Id,
            FiscalDocumentId = fiscalDocument.Id,
            Status = fiscalDocument.Status
        };
    }

    private async Task<IssuerProfile?> ResolveIssuerProfileAsync(PrepareFiscalDocumentCommand command, CancellationToken cancellationToken)
    {
        if (command.IssuerProfileId.HasValue)
        {
            return await _issuerProfileRepository.GetByIdAsync(command.IssuerProfileId.Value, cancellationToken);
        }

        return await _issuerProfileRepository.GetActiveAsync(cancellationToken);
    }

    private async Task<(string Series, string Folio, string? ErrorMessage)> ReserveFiscalFolioAsync(
        IssuerProfile issuerProfile,
        CancellationToken cancellationToken)
    {
        if (!issuerProfile.NextFiscalFolio.HasValue || issuerProfile.NextFiscalFolio.Value <= 0)
        {
            return (string.Empty, string.Empty, "The issuer profile must define a positive next fiscal folio before preparing or stamping CFDI.");
        }

        var normalizedSeries = FiscalMasterDataNormalization.NormalizeOptionalText(issuerProfile.FiscalSeries) ?? string.Empty;
        var normalizedIssuerRfc = FiscalMasterDataNormalization.NormalizeRfc(issuerProfile.Rfc);
        var currentNextFolio = issuerProfile.NextFiscalFolio.Value;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidateFolio = currentNextFolio.ToString(CultureInfo.InvariantCulture);
            var alreadyExists = await _fiscalDocumentRepository.ExistsByIssuerSeriesAndFolioAsync(
                normalizedIssuerRfc,
                normalizedSeries,
                candidateFolio,
                cancellationToken: cancellationToken);

            if (alreadyExists)
            {
                return (string.Empty, string.Empty, $"Configured fiscal folio '{normalizedSeries}{candidateFolio}' is already used. Update the issuer profile next fiscal folio to continue.");
            }

            var reserved = await _issuerProfileRepository.TryAdvanceNextFiscalFolioAsync(
                issuerProfile.Id,
                currentNextFolio,
                currentNextFolio + 1,
                cancellationToken);

            if (reserved)
            {
                return (normalizedSeries, candidateFolio, null);
            }

            issuerProfile = await _issuerProfileRepository.GetByIdAsync(issuerProfile.Id, cancellationToken)
                ?? issuerProfile;

            if (!issuerProfile.NextFiscalFolio.HasValue || issuerProfile.NextFiscalFolio.Value <= 0)
            {
                return (string.Empty, string.Empty, "The issuer profile must define a positive next fiscal folio before preparing or stamping CFDI.");
            }

            currentNextFolio = issuerProfile.NextFiscalFolio.Value;
        }

        return (string.Empty, string.Empty, "Unable to reserve a fiscal folio right now. Retry the operation.");
    }

    private static string? ResolveReceiverCfdiUseCode(PrepareFiscalDocumentCommand command, FiscalReceiver fiscalReceiver)
    {
        return string.IsNullOrWhiteSpace(command.ReceiverCfdiUseCode)
            ? fiscalReceiver.CfdiUseCodeDefault
            : FiscalMasterDataNormalization.NormalizeRequiredCode(command.ReceiverCfdiUseCode);
    }

    private static string? ValidateSpecialFields(
        FiscalReceiver fiscalReceiver,
        IReadOnlyList<PrepareFiscalDocumentSpecialFieldValueCommand>? requestedFields)
    {
        var activeDefinitions = fiscalReceiver.SpecialFieldDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToArray();

        if (activeDefinitions.Length == 0)
        {
            return null;
        }

        var valuesByCode = (requestedFields ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .ToDictionary(
                x => x.FieldCode.Trim().ToUpperInvariant(),
                x => x.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        foreach (var definition in activeDefinitions)
        {
            valuesByCode.TryGetValue(definition.Code, out var capturedValue);
            if (definition.IsRequired && string.IsNullOrWhiteSpace(capturedValue))
            {
                return $"El campo especial '{definition.Label}' es requerido para este receptor.";
            }

            if (definition.MaxLength.HasValue
                && !string.IsNullOrWhiteSpace(capturedValue)
                && capturedValue.Length > definition.MaxLength.Value)
            {
                return $"El campo especial '{definition.Label}' excede la longitud máxima permitida de {definition.MaxLength.Value} caracteres.";
            }

            if (!string.IsNullOrWhiteSpace(capturedValue))
            {
                switch (definition.DataType)
                {
                    case "number" when !decimal.TryParse(capturedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _):
                        return $"El campo especial '{definition.Label}' debe ser numérico.";
                    case "date" when !DateOnly.TryParse(capturedValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                        return $"El campo especial '{definition.Label}' debe ser una fecha válida.";
                }
            }
        }

        return null;
    }

    private static List<FiscalDocumentSpecialFieldValue> BuildSpecialFieldSnapshots(
        FiscalReceiver fiscalReceiver,
        IReadOnlyList<PrepareFiscalDocumentSpecialFieldValueCommand>? requestedFields,
        DateTime now)
    {
        var valuesByCode = (requestedFields ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .ToDictionary(
                x => x.FieldCode.Trim().ToUpperInvariant(),
                x => x.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return fiscalReceiver.SpecialFieldDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(definition => new FiscalDocumentSpecialFieldValue
            {
                FiscalReceiverSpecialFieldDefinitionId = definition.Id,
                FieldCode = definition.Code,
                FieldLabelSnapshot = definition.Label,
                DataType = definition.DataType,
                Value = valuesByCode.TryGetValue(definition.Code, out var value) ? value : string.Empty,
                DisplayOrder = definition.DisplayOrder,
                CreatedAtUtc = now
            })
            .ToList();
    }

    private static bool HasRequiredIssuerFields(IssuerProfile issuerProfile)
    {
        return !string.IsNullOrWhiteSpace(issuerProfile.LegalName)
            && !string.IsNullOrWhiteSpace(issuerProfile.Rfc)
            && !string.IsNullOrWhiteSpace(issuerProfile.FiscalRegimeCode)
            && !string.IsNullOrWhiteSpace(issuerProfile.PostalCode)
            && !string.IsNullOrWhiteSpace(issuerProfile.CfdiVersion)
            && !string.IsNullOrWhiteSpace(issuerProfile.CertificateReference)
            && !string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyReference)
            && !string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyPasswordReference)
            && !string.IsNullOrWhiteSpace(issuerProfile.PacEnvironment);
    }

    private static bool HasRequiredReceiverFields(FiscalReceiver fiscalReceiver, string? receiverCfdiUseCode)
    {
        return !string.IsNullOrWhiteSpace(fiscalReceiver.Rfc)
            && !string.IsNullOrWhiteSpace(fiscalReceiver.LegalName)
            && !string.IsNullOrWhiteSpace(fiscalReceiver.FiscalRegimeCode)
            && !string.IsNullOrWhiteSpace(fiscalReceiver.PostalCode)
            && !string.IsNullOrWhiteSpace(receiverCfdiUseCode);
    }

    private static PrepareFiscalDocumentResult ValidationFailure(long billingDocumentId, string errorMessage)
    {
        return new PrepareFiscalDocumentResult
        {
            Outcome = PrepareFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
