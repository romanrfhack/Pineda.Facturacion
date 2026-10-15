namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableInvoiceReference
{
    public long AccountsReceivableInvoiceId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalSeries { get; init; }

    public string? FiscalFolio { get; init; }

    public string? FiscalUuid { get; init; }
}
