namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchExternalRepBaseDocumentsFilter
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public string? ReceiverRfc { get; init; }

    public string? Query { get; init; }

    public string? ValidationStatus { get; init; }

    public bool? Eligible { get; init; }

    public bool? Blocked { get; init; }
}
