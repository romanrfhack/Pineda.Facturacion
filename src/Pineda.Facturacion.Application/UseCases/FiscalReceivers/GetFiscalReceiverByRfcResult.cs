using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class GetFiscalReceiverByRfcResult
{
    public GetFiscalReceiverByRfcOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public FiscalReceiver? FiscalReceiver { get; set; }
}
