using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class PreparePaymentComplementService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IExternalRepBaseDocumentRepository _externalRepBaseDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PreparePaymentComplementService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IExternalRepBaseDocumentRepository externalRepBaseDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _externalRepBaseDocumentRepository = externalRepBaseDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PreparePaymentComplementResult> ExecuteAsync(PreparePaymentComplementCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable payment id is required.");
        }

        var payment = await _accountsReceivablePaymentRepository.GetByIdAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (payment is null)
        {
            return new PreparePaymentComplementResult
            {
                Outcome = PreparePaymentComplementOutcome.NotFound,
                IsSuccess = false,
                AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                ErrorMessage = $"Accounts receivable payment '{command.AccountsReceivablePaymentId}' was not found."
            };
        }

        if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(payment.CurrencyCode), "MXN", StringComparison.Ordinal))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, $"Current MVP payment complements support MXN only. Payment currency '{payment.CurrencyCode}' is not supported yet.");
        }

        if (!payment.Applications.Any())
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "A payment complement requires at least one persisted payment application.");
        }

        var appliedTotal = payment.Applications.Sum(x => x.AppliedAmount);
        if (appliedTotal != payment.Amount)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Current MVP payment complement preparation requires the payment amount to be fully allocated across persisted applications.");
        }

        var existingDocument = await _paymentComplementDocumentRepository.GetByPaymentIdAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (existingDocument is not null)
        {
            return new PreparePaymentComplementResult
            {
                Outcome = PreparePaymentComplementOutcome.Conflict,
                IsSuccess = false,
                AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                PaymentComplementId = existingDocument.Id,
                Status = existingDocument.Status,
                ErrorMessage = $"Accounts receivable payment '{command.AccountsReceivablePaymentId}' already has a payment complement."
            };
        }

        AnchorSnapshot? anchor = null;
        var relatedDocuments = new List<PaymentComplementRelatedDocument>();
        var now = DateTime.UtcNow;

        foreach (var application in payment.Applications.OrderBy(x => x.ApplicationSequence).ThenBy(x => x.Id))
        {
            var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(application.AccountsReceivableInvoiceId, cancellationToken);
            if (invoice is null)
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Accounts receivable invoice '{application.AccountsReceivableInvoiceId}' was not found.");
            }

            if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(invoice.CurrencyCode), "MXN", StringComparison.Ordinal))
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Current MVP payment complements support MXN only. Invoice currency '{invoice.CurrencyCode}' is not supported yet.");
            }

            PaymentComplementRelatedDocument relatedDocument;
            AnchorSnapshot currentAnchor;
            if (invoice.ExternalRepBaseDocumentId.HasValue)
            {
                var externalDocument = await _externalRepBaseDocumentRepository.GetByIdAsync(invoice.ExternalRepBaseDocumentId.Value, cancellationToken);
                if (externalDocument is null)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{invoice.ExternalRepBaseDocumentId.Value}' was not found.");
                }

                if (externalDocument.ValidationStatus != ExternalRepBaseDocumentValidationStatus.Accepted
                    || externalDocument.SatStatus != ExternalRepBaseDocumentSatStatus.Active)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{externalDocument.Id}' is not in an active validated state eligible for payment complement relation.");
                }

                if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(externalDocument.PaymentMethodSat), "PPD", StringComparison.Ordinal))
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{externalDocument.Id}' does not use MetodoPago PPD.");
                }

                if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(externalDocument.PaymentFormSat), "99", StringComparison.Ordinal))
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{externalDocument.Id}' does not use FormaPago 99.");
                }

                var issuerProfile = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
                if (issuerProfile is null)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, "No active issuer profile is configured to stamp REP for external documents.");
                }

                if (!string.Equals(issuerProfile.Rfc, externalDocument.IssuerRfc, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{externalDocument.Id}' does not match the active issuer profile RFC.");
                }

                var fiscalReceiver = await _fiscalReceiverRepository.GetByRfcAsync(externalDocument.ReceiverRfc, cancellationToken);
                if (fiscalReceiver is null || !fiscalReceiver.IsActive)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"External REP base document '{externalDocument.Id}' does not match an active fiscal receiver in the catalog.");
                }

                currentAnchor = new AnchorSnapshot
                {
                    CfdiVersion = externalDocument.CfdiVersion,
                    IssuerProfileId = issuerProfile.Id,
                    FiscalReceiverId = fiscalReceiver.Id,
                    IssuerRfc = issuerProfile.Rfc,
                    IssuerLegalName = issuerProfile.LegalName,
                    IssuerFiscalRegimeCode = issuerProfile.FiscalRegimeCode,
                    IssuerPostalCode = issuerProfile.PostalCode,
                    ReceiverRfc = fiscalReceiver.Rfc,
                    ReceiverLegalName = fiscalReceiver.LegalName,
                    ReceiverFiscalRegimeCode = fiscalReceiver.FiscalRegimeCode,
                    ReceiverPostalCode = fiscalReceiver.PostalCode,
                    ReceiverCountryCode = fiscalReceiver.CountryCode,
                    ReceiverForeignTaxRegistration = fiscalReceiver.ForeignTaxRegistration,
                    PacEnvironment = issuerProfile.PacEnvironment,
                    CertificateReference = issuerProfile.CertificateReference,
                    PrivateKeyReference = issuerProfile.PrivateKeyReference,
                    PrivateKeyPasswordReference = issuerProfile.PrivateKeyPasswordReference
                };

                relatedDocument = new PaymentComplementRelatedDocument
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    ExternalRepBaseDocumentId = externalDocument.Id,
                    RelatedDocumentUuid = externalDocument.Uuid,
                    CurrencyCode = "MXN",
                    CreatedAtUtc = now
                };
            }
            else
            {
                if (!invoice.FiscalDocumentId.HasValue)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"Accounts receivable invoice '{invoice.Id}' is missing its internal fiscal document reference.");
                }

                var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(invoice.FiscalDocumentId.Value, cancellationToken);
                var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(invoice.FiscalDocumentId.Value, cancellationToken);
                if (fiscalDocument is null || fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"Accounts receivable invoice '{invoice.Id}' does not have the persisted stamped fiscal evidence required for a payment complement.");
                }

                if (fiscalDocument.Status != FiscalDocumentStatus.Stamped && fiscalDocument.Status != FiscalDocumentStatus.CancellationRejected)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, $"Fiscal document '{fiscalDocument.Id}' is not in a stamped lifecycle state eligible for payment complement relation.");
                }

                currentAnchor = new AnchorSnapshot
                {
                    CfdiVersion = fiscalDocument.CfdiVersion,
                    IssuerProfileId = fiscalDocument.IssuerProfileId,
                    FiscalReceiverId = fiscalDocument.FiscalReceiverId,
                    IssuerRfc = fiscalDocument.IssuerRfc,
                    IssuerLegalName = fiscalDocument.IssuerLegalName,
                    IssuerFiscalRegimeCode = fiscalDocument.IssuerFiscalRegimeCode,
                    IssuerPostalCode = fiscalDocument.IssuerPostalCode,
                    ReceiverRfc = fiscalDocument.ReceiverRfc,
                    ReceiverLegalName = fiscalDocument.ReceiverLegalName,
                    ReceiverFiscalRegimeCode = fiscalDocument.ReceiverFiscalRegimeCode,
                    ReceiverPostalCode = fiscalDocument.ReceiverPostalCode,
                    ReceiverCountryCode = fiscalDocument.ReceiverCountryCode,
                    ReceiverForeignTaxRegistration = fiscalDocument.ReceiverForeignTaxRegistration,
                    PacEnvironment = fiscalDocument.PacEnvironment,
                    CertificateReference = fiscalDocument.CertificateReference,
                    PrivateKeyReference = fiscalDocument.PrivateKeyReference,
                    PrivateKeyPasswordReference = fiscalDocument.PrivateKeyPasswordReference
                };

                relatedDocument = new PaymentComplementRelatedDocument
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    FiscalDocumentId = fiscalDocument.Id,
                    FiscalStampId = fiscalStamp.Id,
                    RelatedDocumentUuid = fiscalStamp.Uuid,
                    CurrencyCode = "MXN",
                    CreatedAtUtc = now
                };
            }

            if (anchor is null)
            {
                anchor = currentAnchor;
            }
            else
            {
                if (!string.Equals(anchor.ReceiverRfc, currentAnchor.ReceiverRfc, StringComparison.OrdinalIgnoreCase)
                    || anchor.FiscalReceiverId != currentAnchor.FiscalReceiverId)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, "All invoices applied to one payment complement must belong to the same fiscal receiver.");
                }

                if (!string.Equals(anchor.IssuerRfc, currentAnchor.IssuerRfc, StringComparison.OrdinalIgnoreCase)
                    || anchor.IssuerProfileId != currentAnchor.IssuerProfileId)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, "All invoices applied to one payment complement must belong to the same issuer snapshot.");
                }
            }

            var orderedApplications = invoice.Applications
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.ApplicationSequence)
                .ThenBy(x => x.Id)
                .ToList();

            var index = orderedApplications.FindIndex(x => x.Id == application.Id);
            if (index < 0)
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Could not derive the installment number for invoice '{invoice.Id}' from persisted application history.");
            }

            relatedDocument.InstallmentNumber = index + 1;
            relatedDocument.PreviousBalance = application.PreviousBalance;
            relatedDocument.PaidAmount = application.AppliedAmount;
            relatedDocument.RemainingBalance = application.NewBalance;
            relatedDocuments.Add(relatedDocument);
        }

        if (anchor is null)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "A payment complement requires at least one valid related fiscal invoice.");
        }

        var document = new PaymentComplementDocument
        {
            AccountsReceivablePaymentId = payment.Id,
            Status = PaymentComplementDocumentStatus.ReadyForStamping,
            CfdiVersion = anchor.CfdiVersion,
            DocumentType = "P",
            IssuedAtUtc = command.IssuedAtUtc ?? now,
            PaymentDateUtc = payment.PaymentDateUtc,
            CurrencyCode = "MXN",
            TotalPaymentsAmount = payment.Amount,
            IssuerProfileId = anchor.IssuerProfileId,
            FiscalReceiverId = anchor.FiscalReceiverId,
            IssuerRfc = anchor.IssuerRfc,
            IssuerLegalName = anchor.IssuerLegalName,
            IssuerFiscalRegimeCode = anchor.IssuerFiscalRegimeCode,
            IssuerPostalCode = anchor.IssuerPostalCode,
            ReceiverRfc = anchor.ReceiverRfc,
            ReceiverLegalName = anchor.ReceiverLegalName,
            ReceiverFiscalRegimeCode = anchor.ReceiverFiscalRegimeCode,
            ReceiverPostalCode = anchor.ReceiverPostalCode,
            ReceiverCountryCode = anchor.ReceiverCountryCode,
            ReceiverForeignTaxRegistration = anchor.ReceiverForeignTaxRegistration,
            PacEnvironment = anchor.PacEnvironment,
            CertificateReference = anchor.CertificateReference,
            PrivateKeyReference = anchor.PrivateKeyReference,
            PrivateKeyPasswordReference = anchor.PrivateKeyPasswordReference,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RelatedDocuments = relatedDocuments
        };

        await _paymentComplementDocumentRepository.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PreparePaymentComplementResult
        {
            Outcome = PreparePaymentComplementOutcome.Created,
            IsSuccess = true,
            AccountsReceivablePaymentId = payment.Id,
            PaymentComplementId = document.Id,
            Status = document.Status,
            PaymentComplementDocument = document
        };
    }

    private static PreparePaymentComplementResult ValidationFailure(long paymentId, string errorMessage)
    {
        return new PreparePaymentComplementResult
        {
            Outcome = PreparePaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = errorMessage
        };
    }

    private sealed class AnchorSnapshot
    {
        public string CfdiVersion { get; init; } = string.Empty;

        public long? IssuerProfileId { get; init; }

        public long? FiscalReceiverId { get; init; }

        public string IssuerRfc { get; init; } = string.Empty;

        public string IssuerLegalName { get; init; } = string.Empty;

        public string IssuerFiscalRegimeCode { get; init; } = string.Empty;

        public string IssuerPostalCode { get; init; } = string.Empty;

        public string ReceiverRfc { get; init; } = string.Empty;

        public string ReceiverLegalName { get; init; } = string.Empty;

        public string ReceiverFiscalRegimeCode { get; init; } = string.Empty;

        public string ReceiverPostalCode { get; init; } = string.Empty;

        public string? ReceiverCountryCode { get; init; }

        public string? ReceiverForeignTaxRegistration { get; init; }

        public string PacEnvironment { get; init; } = string.Empty;

        public string CertificateReference { get; init; } = string.Empty;

        public string PrivateKeyReference { get; init; } = string.Empty;

        public string PrivateKeyPasswordReference { get; init; } = string.Empty;
    }
}
