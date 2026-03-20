namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class CreateFiscalReceiverResult
{
    public CreateFiscalReceiverOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? FiscalReceiverId { get; set; }
}
