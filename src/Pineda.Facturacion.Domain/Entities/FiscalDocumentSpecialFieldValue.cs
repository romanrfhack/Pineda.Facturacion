namespace Pineda.Facturacion.Domain.Entities;

public class FiscalDocumentSpecialFieldValue
{
    public long Id { get; set; }

    public long FiscalDocumentId { get; set; }

    public long FiscalReceiverSpecialFieldDefinitionId { get; set; }

    public string FieldCode { get; set; } = string.Empty;

    public string FieldLabelSnapshot { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";

    public string Value { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
