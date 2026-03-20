using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class GetProductFiscalProfileByInternalCodeService
{
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;

    public GetProductFiscalProfileByInternalCodeService(IProductFiscalProfileRepository productFiscalProfileRepository)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
    }

    public async Task<GetProductFiscalProfileByInternalCodeResult> ExecuteAsync(string internalCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(internalCode))
        {
            return new GetProductFiscalProfileByInternalCodeResult
            {
                Outcome = GetProductFiscalProfileByInternalCodeOutcome.NotFound,
                IsSuccess = false
            };
        }

        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(internalCode);
        var productFiscalProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(normalizedInternalCode, cancellationToken);

        return new GetProductFiscalProfileByInternalCodeResult
        {
            Outcome = productFiscalProfile is null ? GetProductFiscalProfileByInternalCodeOutcome.NotFound : GetProductFiscalProfileByInternalCodeOutcome.Found,
            IsSuccess = productFiscalProfile is not null,
            ProductFiscalProfile = productFiscalProfile
        };
    }
}
