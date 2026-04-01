using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class PreparePaymentComplementService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PreparePaymentComplementService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
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

        FiscalDocument? anchorFiscalDocument = null;
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

            var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(invoice.FiscalDocumentId, cancellationToken);
            var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(invoice.FiscalDocumentId, cancellationToken);
            if (fiscalDocument is null || fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Accounts receivable invoice '{invoice.Id}' does not have the persisted stamped fiscal evidence required for a payment complement.");
            }

            if (fiscalDocument.Status != FiscalDocumentStatus.Stamped && fiscalDocument.Status != FiscalDocumentStatus.CancellationRejected)
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Fiscal document '{fiscalDocument.Id}' is not in a stamped lifecycle state eligible for payment complement relation.");
            }

            if (anchorFiscalDocument is null)
            {
                anchorFiscalDocument = fiscalDocument;
            }
            else
            {
                if (!string.Equals(anchorFiscalDocument.ReceiverRfc, fiscalDocument.ReceiverRfc, StringComparison.OrdinalIgnoreCase)
                    || anchorFiscalDocument.FiscalReceiverId != fiscalDocument.FiscalReceiverId)
                {
                    return ValidationFailure(command.AccountsReceivablePaymentId, "All invoices applied to one payment complement must belong to the same fiscal receiver.");
                }

                if (!string.Equals(anchorFiscalDocument.IssuerRfc, fiscalDocument.IssuerRfc, StringComparison.OrdinalIgnoreCase))
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

            relatedDocuments.Add(new PaymentComplementRelatedDocument
            {
                AccountsReceivableInvoiceId = invoice.Id,
                FiscalDocumentId = fiscalDocument.Id,
                FiscalStampId = fiscalStamp.Id,
                RelatedDocumentUuid = fiscalStamp.Uuid,
                InstallmentNumber = index + 1,
                PreviousBalance = application.PreviousBalance,
                PaidAmount = application.AppliedAmount,
                RemainingBalance = application.NewBalance,
                CurrencyCode = "MXN",
                CreatedAtUtc = now
            });
        }

        if (anchorFiscalDocument is null)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "A payment complement requires at least one valid related fiscal invoice.");
        }

        var document = new PaymentComplementDocument
        {
            AccountsReceivablePaymentId = payment.Id,
            Status = PaymentComplementDocumentStatus.ReadyForStamping,
            CfdiVersion = anchorFiscalDocument.CfdiVersion,
            DocumentType = "P",
            IssuedAtUtc = command.IssuedAtUtc ?? now,
            PaymentDateUtc = payment.PaymentDateUtc,
            CurrencyCode = "MXN",
            TotalPaymentsAmount = payment.Amount,
            IssuerProfileId = anchorFiscalDocument.IssuerProfileId,
            FiscalReceiverId = anchorFiscalDocument.FiscalReceiverId,
            IssuerRfc = anchorFiscalDocument.IssuerRfc,
            IssuerLegalName = anchorFiscalDocument.IssuerLegalName,
            IssuerFiscalRegimeCode = anchorFiscalDocument.IssuerFiscalRegimeCode,
            IssuerPostalCode = anchorFiscalDocument.IssuerPostalCode,
            ReceiverRfc = anchorFiscalDocument.ReceiverRfc,
            ReceiverLegalName = anchorFiscalDocument.ReceiverLegalName,
            ReceiverFiscalRegimeCode = anchorFiscalDocument.ReceiverFiscalRegimeCode,
            ReceiverPostalCode = anchorFiscalDocument.ReceiverPostalCode,
            ReceiverCountryCode = anchorFiscalDocument.ReceiverCountryCode,
            ReceiverForeignTaxRegistration = anchorFiscalDocument.ReceiverForeignTaxRegistration,
            PacEnvironment = anchorFiscalDocument.PacEnvironment,
            CertificateReference = anchorFiscalDocument.CertificateReference,
            PrivateKeyReference = anchorFiscalDocument.PrivateKeyReference,
            PrivateKeyPasswordReference = anchorFiscalDocument.PrivateKeyPasswordReference,
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
}
