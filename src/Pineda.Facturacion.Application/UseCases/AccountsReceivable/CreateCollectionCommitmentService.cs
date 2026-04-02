using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionCommitmentService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCollectionCommitmentService(
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivableCollectionRepository collectionRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _collectionRepository = collectionRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateCollectionCommitmentResult> ExecuteAsync(CreateCollectionCommitmentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivableInvoiceId <= 0)
        {
            return ValidationFailure("Accounts receivable invoice id is required.");
        }

        if (command.PromisedAmount <= 0m)
        {
            return ValidationFailure("Promised amount must be greater than zero.");
        }

        if (command.PromisedDateUtc.Date < DateTime.UtcNow.Date)
        {
            return ValidationFailure("Promised date cannot be in the past for this MVP flow.");
        }

        var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(command.AccountsReceivableInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return new CreateCollectionCommitmentResult
            {
                Outcome = CreateCollectionCommitmentOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Accounts receivable invoice '{command.AccountsReceivableInvoiceId}' was not found."
            };
        }

        if (invoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
        {
            return Conflict("Cancelled accounts receivable invoices cannot register collection commitments.");
        }

        if (invoice.OutstandingBalance <= 0m || invoice.Status == AccountsReceivableInvoiceStatus.Paid)
        {
            return Conflict("Paid accounts receivable invoices cannot register collection commitments.");
        }

        if (command.PromisedAmount > invoice.OutstandingBalance)
        {
            return Conflict("Promised amount cannot exceed the outstanding balance.");
        }

        var now = DateTime.UtcNow;
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var commitment = new CollectionCommitment
        {
            AccountsReceivableInvoiceId = invoice.Id,
            PromisedAmount = command.PromisedAmount,
            PromisedDateUtc = command.PromisedDateUtc,
            Status = CollectionCommitmentStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUsername = currentUser.Username ?? currentUser.DisplayName
        };

        await _collectionRepository.AddCommitmentAsync(commitment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCollectionCommitmentResult
        {
            Outcome = CreateCollectionCommitmentOutcome.Created,
            IsSuccess = true,
            Commitment = AccountsReceivableCollectionProjectionBuilder.MapCommitment(commitment, invoice.OutstandingBalance, invoice.Status.ToString(), now)
        };
    }

    private static CreateCollectionCommitmentResult ValidationFailure(string errorMessage)
        => new()
        {
            Outcome = CreateCollectionCommitmentOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };

    private static CreateCollectionCommitmentResult Conflict(string errorMessage)
        => new()
        {
            Outcome = CreateCollectionCommitmentOutcome.Conflict,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
