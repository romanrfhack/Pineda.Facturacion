namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class UpdateFiscalReceiverResult
{
    public UpdateFiscalReceiverOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? FiscalReceiverId { get; set; }
}
