namespace Pineda.Facturacion.Domain.Entities;

public class FiscalReceiverSpecialFieldDefinition
{
    public long Id { get; set; }

    public long FiscalReceiverId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";

    public int? MaxLength { get; set; }

    public string? HelpText { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
