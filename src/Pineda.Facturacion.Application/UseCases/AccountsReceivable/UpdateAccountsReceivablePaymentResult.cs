using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentResult
{
    public UpdateAccountsReceivablePaymentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public IReadOnlyList<string> UpdatedFields { get; set; } = [];

    public AccountsReceivablePaymentMutationSnapshot? PreviousPayment { get; set; }

    public AccountsReceivablePayment? AccountsReceivablePayment { get; set; }
}
