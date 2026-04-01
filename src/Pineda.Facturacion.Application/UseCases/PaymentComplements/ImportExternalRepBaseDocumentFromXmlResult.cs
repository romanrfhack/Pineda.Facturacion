namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ImportExternalRepBaseDocumentFromXmlResult
{
    public ImportExternalRepBaseDocumentFromXmlOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public long? ExternalRepBaseDocumentId { get; init; }

    public string ValidationStatus { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string ReasonMessage { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public string? Uuid { get; init; }

    public string? IssuerRfc { get; init; }

    public string? ReceiverRfc { get; init; }

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public string? CurrencyCode { get; init; }

    public decimal? Total { get; init; }

    public bool IsDuplicate { get; init; }
}
