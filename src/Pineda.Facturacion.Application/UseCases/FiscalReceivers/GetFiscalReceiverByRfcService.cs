using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class GetFiscalReceiverByRfcService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;

    public GetFiscalReceiverByRfcService(IFiscalReceiverRepository fiscalReceiverRepository)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
    }

    public async Task<GetFiscalReceiverByRfcResult> ExecuteAsync(string rfc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rfc))
        {
            return new GetFiscalReceiverByRfcResult
            {
                Outcome = GetFiscalReceiverByRfcOutcome.NotFound,
                IsSuccess = false
            };
        }

        var normalizedRfc = FiscalMasterDataNormalization.NormalizeRfc(rfc);
        var fiscalReceiver = await _fiscalReceiverRepository.GetByRfcAsync(normalizedRfc, cancellationToken);

        return new GetFiscalReceiverByRfcResult
        {
            Outcome = fiscalReceiver is null ? GetFiscalReceiverByRfcOutcome.NotFound : GetFiscalReceiverByRfcOutcome.Found,
            IsSuccess = fiscalReceiver is not null,
            FiscalReceiver = fiscalReceiver
        };
    }
}
