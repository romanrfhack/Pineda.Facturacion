namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ImportExternalRepBaseDocumentFromXmlCommand
{
    public string SourceFileName { get; set; } = string.Empty;

    public byte[] FileContent { get; set; } = [];
}
