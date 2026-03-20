namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class PreviewFiscalReceiverImportFromExcelCommand
{
    public string SourceFileName { get; set; } = string.Empty;

    public byte[] FileContent { get; set; } = [];
}
