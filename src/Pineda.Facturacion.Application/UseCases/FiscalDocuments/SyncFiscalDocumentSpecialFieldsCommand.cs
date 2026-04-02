namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SyncFiscalDocumentSpecialFieldsCommand
{
    public long FiscalDocumentId { get; set; }

    public IReadOnlyList<SyncFiscalDocumentSpecialFieldValueCommand> SpecialFields { get; set; } = [];
}

public sealed class SyncFiscalDocumentSpecialFieldValueCommand
{
    public string FieldCode { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
