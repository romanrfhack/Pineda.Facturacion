using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivableInvoiceRepository
{
    Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default);

    Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default);
}
