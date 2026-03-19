namespace Pineda.Facturacion.Application.UseCases.CreateBillingDocument;

public class CreateBillingDocumentCommand
{
    public long SalesOrderId { get; set; }

    public string DocumentType { get; set; } = string.Empty;
}
