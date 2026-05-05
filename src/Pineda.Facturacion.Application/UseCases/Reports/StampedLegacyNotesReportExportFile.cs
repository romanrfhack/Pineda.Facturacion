namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class StampedLegacyNotesReportExportFile
{
    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public byte[] Content { get; init; } = [];
}
