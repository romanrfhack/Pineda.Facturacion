using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.CreateBillingDocument;

public class CreateBillingDocumentService
{
    private const string DefaultTaxObjectCode = "02";

    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBillingDocumentService(
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _billingDocumentRepository = billingDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateBillingDocumentResult> ExecuteAsync(
        CreateBillingDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.SalesOrderId <= 0)
        {
            return CreateFailureResult(command.SalesOrderId, "Sales order id is required.", CreateBillingDocumentOutcome.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(command.DocumentType))
        {
            return CreateFailureResult(command.SalesOrderId, "Document type is required.", CreateBillingDocumentOutcome.ValidationFailed);
        }

        var salesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(command.SalesOrderId, cancellationToken);

        if (salesOrder is null)
        {
            return new CreateBillingDocumentResult
            {
                Outcome = CreateBillingDocumentOutcome.NotFound,
                IsSuccess = false,
                SalesOrderId = command.SalesOrderId,
                ErrorMessage = $"Sales order '{command.SalesOrderId}' was not found."
            };
        }

        var existingBillingDocument = await _billingDocumentRepository.GetBySalesOrderIdAsync(command.SalesOrderId, cancellationToken);

        if (existingBillingDocument is not null)
        {
            return new CreateBillingDocumentResult
            {
                Outcome = CreateBillingDocumentOutcome.Conflict,
                IsSuccess = false,
                SalesOrderId = command.SalesOrderId,
                BillingDocumentId = existingBillingDocument.Id,
                BillingDocumentStatus = existingBillingDocument.Status,
                ErrorMessage = $"Sales order '{command.SalesOrderId}' already has a billing document."
            };
        }

        BillingDocument billingDocument;

        try
        {
            var now = DateTime.UtcNow;
            billingDocument = MapBillingDocument(salesOrder, command.DocumentType, now);
        }
        catch (InvalidOperationException exception)
        {
            return CreateFailureResult(command.SalesOrderId, exception.Message, CreateBillingDocumentOutcome.ValidationFailed);
        }

        await _billingDocumentRepository.AddAsync(billingDocument, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateBillingDocumentResult
        {
            Outcome = CreateBillingDocumentOutcome.Created,
            IsSuccess = true,
            SalesOrderId = command.SalesOrderId,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status
        };
    }

    private static CreateBillingDocumentResult CreateFailureResult(
        long salesOrderId,
        string errorMessage,
        CreateBillingDocumentOutcome outcome)
    {
        return new CreateBillingDocumentResult
        {
            Outcome = outcome,
            IsSuccess = false,
            SalesOrderId = salesOrderId,
            ErrorMessage = errorMessage
        };
    }

    private static BillingDocument MapBillingDocument(SalesOrder salesOrder, string documentType, DateTime now)
    {
        var normalizedCurrencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(salesOrder.CurrencyCode);

        if (normalizedCurrencyCode != "MXN")
        {
            throw new InvalidOperationException(
                $"Current MVP BillingDocument creation supports MXN only. Sales order '{salesOrder.Id}' has currency '{normalizedCurrencyCode}'.");
        }

        return new BillingDocument
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = documentType,
            Status = BillingDocumentStatus.Draft,
            PaymentCondition = salesOrder.PaymentCondition,
            CurrencyCode = normalizedCurrencyCode,
            ExchangeRate = 1m,
            Subtotal = salesOrder.Subtotal,
            DiscountTotal = salesOrder.DiscountTotal,
            TaxTotal = salesOrder.TaxTotal,
            Total = salesOrder.Total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items = salesOrder.Items.Select(item => new BillingDocumentItem
            {
                LineNumber = item.LineNumber,
                Sku = item.Sku,
                ProductInternalCode = string.IsNullOrWhiteSpace(item.Sku)
                    ? null
                    : FiscalMasterDataNormalization.NormalizeRequiredCode(item.Sku),
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxRate = item.TaxRate,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal,
                SatProductServiceCode = item.SatProductServiceCode,
                SatUnitCode = item.SatUnitCode,
                TaxObjectCode = DefaultTaxObjectCode
            }).ToList()
        };
    }
}
