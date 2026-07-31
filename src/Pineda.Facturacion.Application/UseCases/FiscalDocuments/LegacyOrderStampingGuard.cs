using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class LegacyOrderStampingGuard : ILegacyOrderStampingGuard
{
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;

    public LegacyOrderStampingGuard(
        ILegacyImportRecordRepository legacyImportRecordRepository,
        ILegacyOrderReader legacyOrderReader,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository)
    {
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _legacyOrderReader = legacyOrderReader;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
    }

    public async Task<LegacyOrderStampingValidationResult> ValidateAsync(
        long billingDocumentId,
        CancellationToken cancellationToken = default)
    {
        if (billingDocumentId <= 0)
        {
            return Unavailable("No fue posible identificar el documento de facturación para validar sus órdenes.");
        }

        try
        {
            var salesOrders = await _salesOrderSnapshotRepository.GetByBillingDocumentIdWithItemsAsync(
                billingDocumentId,
                cancellationToken);
            if (salesOrders.Count == 0)
            {
                return Unavailable("No fue posible identificar las órdenes asociadas al documento antes del timbrado.");
            }

            var validationTargets = new List<LegacyOrderStampingBlockingOrder>(salesOrders.Count);
            foreach (var salesOrder in salesOrders)
            {
                var importRecord = await _legacyImportRecordRepository.GetByIdAsync(
                    salesOrder.LegacyImportRecordId,
                    cancellationToken);
                var legacyOrderId = string.IsNullOrWhiteSpace(importRecord?.SourceDocumentId)
                    ? salesOrder.LegacyOrderNumber?.Trim()
                    : importRecord.SourceDocumentId.Trim();
                if (string.IsNullOrWhiteSpace(legacyOrderId))
                {
                    return Unavailable($"No fue posible identificar la orden legacy asociada a la orden interna '{salesOrder.Id}'.");
                }

                validationTargets.Add(new LegacyOrderStampingBlockingOrder
                {
                    SalesOrderId = salesOrder.Id,
                    LegacyOrderId = legacyOrderId
                });
            }

            var canceledOrderIds = await _legacyOrderReader.FindCanceledOrderIdsAsync(
                validationTargets.Select(x => x.LegacyOrderId).ToArray(),
                cancellationToken);
            var canceledOrderIdSet = canceledOrderIds.ToHashSet(StringComparer.Ordinal);
            var blockingOrders = validationTargets
                .Where(x => canceledOrderIdSet.Contains(x.LegacyOrderId))
                .OrderBy(x => x.LegacyOrderId, StringComparer.Ordinal)
                .ToArray();

            return blockingOrders.Length == 0
                ? new LegacyOrderStampingValidationResult
                {
                    Outcome = LegacyOrderStampingValidationOutcome.Valid
                }
                : new LegacyOrderStampingValidationResult
                {
                    Outcome = LegacyOrderStampingValidationOutcome.BlockedByCanceledOrders,
                    ErrorMessage = BuildBlockedMessage(blockingOrders),
                    BlockingCanceledOrders = blockingOrders
                };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable("No fue posible validar el estado vigente de las órdenes en el sistema de origen. No se envió el CFDI al PAC; intenta nuevamente.");
        }
    }

    private static string BuildBlockedMessage(IReadOnlyList<LegacyOrderStampingBlockingOrder> blockingOrders)
    {
        var orderIds = string.Join(", ", blockingOrders.Select(x => x.LegacyOrderId));
        return blockingOrders.Count == 1
            ? $"La orden {orderIds} está cancelada en el sistema de origen. Retírala del documento antes de timbrar."
            : $"Las órdenes {orderIds} están canceladas en el sistema de origen. Retíralas del documento antes de timbrar.";
    }

    private static LegacyOrderStampingValidationResult Unavailable(string errorMessage)
    {
        return new LegacyOrderStampingValidationResult
        {
            Outcome = LegacyOrderStampingValidationOutcome.ValidationUnavailable,
            ErrorMessage = errorMessage
        };
    }
}
