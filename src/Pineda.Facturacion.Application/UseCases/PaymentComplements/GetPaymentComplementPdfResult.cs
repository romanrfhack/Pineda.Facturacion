namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum GetPaymentComplementPdfOutcome
{
    Found = 0,
    NotFound = 1,
    NotStamped = 2,
    RenderFailed = 3
}

public class GetPaymentComplementPdfResult
{
    public GetPaymentComplementPdfOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public byte[]? Content { get; set; }

    public string? FileName { get; set; }

    public string? ErrorMessage { get; set; }
}
