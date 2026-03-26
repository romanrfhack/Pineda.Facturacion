namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface ISatCatalogDescriptionProvider
{
    string FormatFiscalRegime(string? code);

    string FormatCfdiUse(string? code);

    string FormatPaymentForm(string? code);

    string FormatPaymentMethod(string? code);

    string FormatExportCode(string? code);
}
