using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ImportLegacyFiscalProductMappingsFromCsvResult
{
    public ImportLegacyFiscalProductMappingsFromCsvOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public bool WasAlreadyImported { get; init; }

    public string? ErrorMessage { get; init; }

    public FiscalProductMappingImportBatch? Batch { get; init; }
}
