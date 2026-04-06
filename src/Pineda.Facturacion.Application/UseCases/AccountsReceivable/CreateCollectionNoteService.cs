using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionNoteService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCollectionNoteService(
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

    public async Task<CreateCollectionNoteResult> ExecuteAsync(CreateCollectionNoteCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivableInvoiceId <= 0)
        {
            return ValidationFailure("Accounts receivable invoice id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return ValidationFailure("Collection note content is required.");
        }

        if (!Enum.TryParse<CollectionNoteType>(command.NoteType, true, out var noteType))
        {
            return ValidationFailure("Collection note type is invalid.");
        }

        var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(command.AccountsReceivableInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return new CreateCollectionNoteResult
            {
                Outcome = CreateCollectionNoteOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Accounts receivable invoice '{command.AccountsReceivableInvoiceId}' was not found."
            };
        }

        if (invoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
        {
            return Conflict("Cancelled accounts receivable invoices cannot register collection notes.");
        }

        var currentUser = _currentUserAccessor.GetCurrentUser();
        var note = new CollectionNote
        {
            AccountsReceivableInvoiceId = invoice.Id,
            NoteType = noteType,
            Content = command.Content.Trim(),
            NextFollowUpAtUtc = command.NextFollowUpAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUsername = currentUser.Username ?? currentUser.DisplayName
        };

        await _collectionRepository.AddNoteAsync(note, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCollectionNoteResult
        {
            Outcome = CreateCollectionNoteOutcome.Created,
            IsSuccess = true,
            Note = AccountsReceivableCollectionProjectionBuilder.MapNote(note)
        };
    }

    private static CreateCollectionNoteResult ValidationFailure(string errorMessage)
        => new()
        {
            Outcome = CreateCollectionNoteOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };

    private static CreateCollectionNoteResult Conflict(string errorMessage)
        => new()
        {
            Outcome = CreateCollectionNoteOutcome.Conflict,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
