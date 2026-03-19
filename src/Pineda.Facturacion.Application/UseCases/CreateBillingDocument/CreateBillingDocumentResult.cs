using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.CreateBillingDocument;

public class CreateBillingDocumentResult
{
    public CreateBillingDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long SalesOrderId { get; set; }

    public long? BillingDocumentId { get; set; }

    public BillingDocumentStatus? BillingDocumentStatus { get; set; }
}
