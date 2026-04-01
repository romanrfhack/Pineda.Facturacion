namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchExternalRepBaseDocumentsDataFilter
{
    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public string? ReceiverRfc { get; init; }

    public string? Query { get; init; }
}
