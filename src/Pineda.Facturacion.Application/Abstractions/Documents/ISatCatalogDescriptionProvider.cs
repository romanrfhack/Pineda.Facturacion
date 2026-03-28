namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface ISatCatalogDescriptionProvider
{
    IReadOnlyDictionary<string, string> GetPaymentForms();

    IReadOnlyDictionary<string, string> GetPaymentMethods();

    string FormatFiscalRegime(string? code);

    string FormatCfdiUse(string? code);

    string FormatPaymentForm(string? code);

    string FormatPaymentMethod(string? code);

    string FormatExportCode(string? code);
}
