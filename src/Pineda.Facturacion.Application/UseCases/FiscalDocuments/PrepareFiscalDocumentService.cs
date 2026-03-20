using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class PrepareFiscalDocumentService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PrepareFiscalDocumentService(
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
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

        if (command.IsCreditSale)
        {
            if (command.CreditDays is null or <= 0)
            {
                return ValidationFailure(command.BillingDocumentId, "Credit days must be greater than zero for credit sales.");
            }

            if (!string.Equals(command.PaymentMethodSat.Trim(), "PPD", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationFailure(command.BillingDocumentId, "Credit sales require payment method SAT 'PPD'.");
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

        var fiscalDocument = new FiscalDocument
        {
            BillingDocumentId = billingDocument.Id,
            IssuerProfileId = issuerProfile.Id,
            FiscalReceiverId = fiscalReceiver.Id,
            Status = FiscalDocumentStatus.ReadyForStamping,
            CfdiVersion = issuerProfile.CfdiVersion,
            DocumentType = billingDocument.DocumentType,
            Series = billingDocument.Series,
            Folio = billingDocument.Folio,
            IssuedAtUtc = command.IssuedAtUtc ?? now,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            PaymentMethodSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentMethodSat),
            PaymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentFormSat),
            PaymentCondition = FiscalMasterDataNormalization.NormalizeOptionalText(command.PaymentCondition) ?? billingDocument.PaymentCondition,
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
            Items = itemResults
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

    private static string? ResolveReceiverCfdiUseCode(PrepareFiscalDocumentCommand command, FiscalReceiver fiscalReceiver)
    {
        return string.IsNullOrWhiteSpace(command.ReceiverCfdiUseCode)
            ? fiscalReceiver.CfdiUseCodeDefault
            : FiscalMasterDataNormalization.NormalizeRequiredCode(command.ReceiverCfdiUseCode);
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
