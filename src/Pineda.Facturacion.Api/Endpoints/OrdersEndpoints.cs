using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Api.Endpoints;

public static class OrdersEndpoints
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapPost("/{legacyOrderId}/import", ImportOrderAsync)
            .WithName("ImportLegacyOrder")
            .WithSummary("Import a legacy order snapshot")
            .WithDescription("Reads a single legacy order, creates a snapshot in the billing database, and returns the import result.")
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status200OK)
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status404NotFound)
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Results<Ok<ImportLegacyOrderResponse>, NotFound<ImportLegacyOrderResponse>, Conflict<ImportLegacyOrderResponse>>> ImportOrderAsync(
        string legacyOrderId,
        ImportLegacyOrderService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new ImportLegacyOrderCommand
            {
                // Source system naming is not formalized in docs yet, so this stays as an explicit local assumption.
                SourceSystem = LegacySourceSystem,
                SourceTable = LegacyOrdersSourceTable,
                LegacyOrderId = legacyOrderId
            },
            cancellationToken);

        var response = new ImportLegacyOrderResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            IsIdempotent = result.IsIdempotent,
            ErrorMessage = result.ErrorMessage,
            SourceSystem = result.SourceSystem,
            SourceTable = result.SourceTable,
            LegacyOrderId = result.LegacyOrderId,
            SourceHash = result.SourceHash,
            LegacyImportRecordId = result.LegacyImportRecordId,
            SalesOrderId = result.SalesOrderId,
            ImportStatus = result.ImportStatus?.ToString()
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "Order.Import",
            "SalesOrder",
            result.SalesOrderId?.ToString() ?? legacyOrderId,
            result.Outcome.ToString(),
            new { legacyOrderId, result.SourceSystem, result.SourceTable },
            new { result.SalesOrderId, result.LegacyImportRecordId, result.ImportStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ImportLegacyOrderOutcome.Imported => TypedResults.Ok(response),
            ImportLegacyOrderOutcome.Idempotent => TypedResults.Ok(response),
            ImportLegacyOrderOutcome.NotFound => TypedResults.NotFound(response),
            ImportLegacyOrderOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.Conflict(response)
        };
    }

    public sealed class ImportLegacyOrderResponse
    {
        public string Outcome { get; init; } = string.Empty;

        public bool IsSuccess { get; init; }

        public bool IsIdempotent { get; init; }

        public string? ErrorMessage { get; init; }

        public string SourceSystem { get; init; } = string.Empty;

        public string SourceTable { get; init; } = string.Empty;

        public string LegacyOrderId { get; init; } = string.Empty;

        public string SourceHash { get; init; } = string.Empty;

        public long? LegacyImportRecordId { get; init; }

        public long? SalesOrderId { get; init; }

        public string? ImportStatus { get; init; }
    }
}
