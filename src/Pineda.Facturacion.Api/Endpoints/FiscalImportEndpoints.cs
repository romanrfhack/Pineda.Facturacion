using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class FiscalImportEndpoints
{
    public static IEndpointRouteBuilder MapFiscalImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal/imports")
            .WithTags("Catalogs")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapPost("/receivers/preview", PreviewFiscalReceiverImportAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("PreviewFiscalReceiverImport")
            .WithSummary("Preview fiscal receiver import from Excel")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status200OK)
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/receivers/batches/{batchId:long}", GetFiscalReceiverImportBatchAsync)
            .WithName("GetFiscalReceiverImportBatch")
            .WithSummary("Get a fiscal receiver import batch summary")
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/receivers/batches/{batchId:long}/rows", ListFiscalReceiverImportRowsAsync)
            .WithName("ListFiscalReceiverImportRows")
            .WithSummary("List fiscal receiver import rows")
            .Produces<IReadOnlyList<FiscalReceiverImportRowResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/receivers/batches/{batchId:long}/apply", ApplyFiscalReceiverImportBatchAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("ApplyFiscalReceiverImportBatch")
            .WithSummary("Apply validated fiscal receiver staging rows into master data")
            .Produces<ApplyImportBatchResponse>(StatusCodes.Status200OK)
            .Produces<ApplyImportBatchResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/products/preview", PreviewProductFiscalProfileImportAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("PreviewProductFiscalProfileImport")
            .WithSummary("Preview product fiscal profile import from Excel")
            .Accepts<PreviewProductFiscalProfileImportRequest>("multipart/form-data")
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status200OK)
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/products/batches/{batchId:long}", GetProductFiscalProfileImportBatchAsync)
            .WithName("GetProductFiscalProfileImportBatch")
            .WithSummary("Get a product fiscal profile import batch summary")
            .Produces<ImportBatchSummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/products/batches/{batchId:long}/rows", ListProductFiscalProfileImportRowsAsync)
            .WithName("ListProductFiscalProfileImportRows")
            .WithSummary("List product fiscal profile import rows")
            .Produces<IReadOnlyList<ProductFiscalProfileImportRowResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/products/batches/{batchId:long}/apply", ApplyProductFiscalProfileImportBatchAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("ApplyProductFiscalProfileImportBatch")
            .WithSummary("Apply validated product fiscal profile staging rows into master data")
            .Produces<ApplyImportBatchResponse>(StatusCodes.Status200OK)
            .Produces<ApplyImportBatchResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<ImportBatchSummaryResponse>, BadRequest<ImportBatchSummaryResponse>>> PreviewFiscalReceiverImportAsync(
        IFormFile file,
        PreviewFiscalReceiverImportFromExcelService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var fileBytes = await ReadFileAsync(file, cancellationToken);
        var result = await service.ExecuteAsync(new PreviewFiscalReceiverImportFromExcelCommand
        {
            SourceFileName = file.FileName,
            FileContent = fileBytes
        }, cancellationToken);

        var response = MapBatchSummary(result.Batch, result.ErrorMessage);
        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalReceiverImport.Preview",
            "FiscalReceiverImportBatch",
            result.Batch?.Id.ToString(),
            result.Outcome.ToString(),
            new { file.FileName, file.Length },
            new { result.Batch?.Id, result.Batch?.Status, result.Batch?.TotalRows, result.Batch?.ValidRows, result.Batch?.InvalidRows },
            result.ErrorMessage,
            cancellationToken);
        return result.Outcome == PreviewFiscalReceiverImportFromExcelOutcome.Completed
            ? TypedResults.Ok(response)
            : TypedResults.BadRequest(response);
    }

    private static async Task<Results<Ok<ImportBatchSummaryResponse>, NotFound>> GetFiscalReceiverImportBatchAsync(
        long batchId,
        GetFiscalReceiverImportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(batchId, cancellationToken);
        if (result.Outcome == GetFiscalReceiverImportBatchOutcome.NotFound || result.Batch is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapBatchSummary(result.Batch));
    }

    private static async Task<Results<Ok<IReadOnlyList<FiscalReceiverImportRowResponse>>, NotFound>> ListFiscalReceiverImportRowsAsync(
        long batchId,
        ListFiscalReceiverImportRowsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(batchId, cancellationToken);
        if (result.Outcome == ListFiscalReceiverImportRowsOutcome.NotFound)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<FiscalReceiverImportRowResponse> items = result.Rows.Select(MapReceiverRow).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<ApplyImportBatchResponse>, BadRequest<ApplyImportBatchResponse>, NotFound>> ApplyFiscalReceiverImportBatchAsync(
        long batchId,
        ApplyImportBatchRequest request,
        ApplyFiscalReceiverImportBatchService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batchId,
            ApplyMode = request.ApplyMode,
            SelectedRowNumbers = request.SelectedRowNumbers,
            StopOnFirstError = request.StopOnFirstError
        }, cancellationToken);

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalReceiverImport.Apply",
            "FiscalReceiverImportBatch",
            batchId.ToString(),
            result.Outcome.ToString(),
            new { batchId, request.ApplyMode, request.SelectedRowNumbers, request.StopOnFirstError },
            new { result.AppliedRows, result.FailedRows, result.SkippedRows, result.AlreadyAppliedRows, result.LastAppliedAtUtc },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ApplyFiscalReceiverImportBatchOutcome.Applied => TypedResults.Ok(MapApplyResponse(result)),
            ApplyFiscalReceiverImportBatchOutcome.InvalidBatchState => TypedResults.BadRequest(MapApplyResponse(result)),
            ApplyFiscalReceiverImportBatchOutcome.NotFound => TypedResults.NotFound(),
            _ => TypedResults.BadRequest(MapApplyResponse(result))
        };
    }

    private static async Task<Results<Ok<ImportBatchSummaryResponse>, BadRequest<ImportBatchSummaryResponse>>> PreviewProductFiscalProfileImportAsync(
        [FromForm] PreviewProductFiscalProfileImportRequest request,
        PreviewProductFiscalProfileImportFromExcelService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var fileBytes = await ReadFileAsync(request.File, cancellationToken);
        var result = await service.ExecuteAsync(new PreviewProductFiscalProfileImportFromExcelCommand
        {
            SourceFileName = request.File.FileName,
            FileContent = fileBytes,
            DefaultTaxObjectCode = request.DefaultTaxObjectCode,
            DefaultVatRate = request.DefaultVatRate,
            DefaultUnitText = request.DefaultUnitText
        }, cancellationToken);

        var response = MapBatchSummary(result.Batch, result.ErrorMessage);
        await AuditApiHelper.RecordAsync(
            auditService,
            "ProductFiscalProfileImport.Preview",
            "ProductFiscalProfileImportBatch",
            result.Batch?.Id.ToString(),
            result.Outcome.ToString(),
            new
            {
                request.File.FileName,
                request.File.Length,
                request.DefaultTaxObjectCode,
                request.DefaultVatRate,
                request.DefaultUnitText
            },
            new { result.Batch?.Id, result.Batch?.Status, result.Batch?.TotalRows, result.Batch?.ValidRows, result.Batch?.InvalidRows },
            result.ErrorMessage,
            cancellationToken);
        return result.Outcome == PreviewProductFiscalProfileImportFromExcelOutcome.Completed
            ? TypedResults.Ok(response)
            : TypedResults.BadRequest(response);
    }

    private static async Task<Results<Ok<ImportBatchSummaryResponse>, NotFound>> GetProductFiscalProfileImportBatchAsync(
        long batchId,
        GetProductFiscalProfileImportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(batchId, cancellationToken);
        if (result.Outcome == GetProductFiscalProfileImportBatchOutcome.NotFound || result.Batch is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapBatchSummary(result.Batch));
    }

    private static async Task<Results<Ok<IReadOnlyList<ProductFiscalProfileImportRowResponse>>, NotFound>> ListProductFiscalProfileImportRowsAsync(
        long batchId,
        ListProductFiscalProfileImportRowsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(batchId, cancellationToken);
        if (result.Outcome == ListProductFiscalProfileImportRowsOutcome.NotFound)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<ProductFiscalProfileImportRowResponse> items = result.Rows.Select(MapProductRow).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<ApplyImportBatchResponse>, BadRequest<ApplyImportBatchResponse>, NotFound>> ApplyProductFiscalProfileImportBatchAsync(
        long batchId,
        ApplyImportBatchRequest request,
        ApplyProductFiscalProfileImportBatchService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batchId,
            ApplyMode = request.ApplyMode,
            SelectedRowNumbers = request.SelectedRowNumbers,
            StopOnFirstError = request.StopOnFirstError
        }, cancellationToken);

        await AuditApiHelper.RecordAsync(
            auditService,
            "ProductFiscalProfileImport.Apply",
            "ProductFiscalProfileImportBatch",
            batchId.ToString(),
            result.Outcome.ToString(),
            new { batchId, request.ApplyMode, request.SelectedRowNumbers, request.StopOnFirstError },
            new { result.AppliedRows, result.FailedRows, result.SkippedRows, result.AlreadyAppliedRows, result.LastAppliedAtUtc },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ApplyProductFiscalProfileImportBatchOutcome.Applied => TypedResults.Ok(MapApplyResponse(result)),
            ApplyProductFiscalProfileImportBatchOutcome.InvalidBatchState => TypedResults.BadRequest(MapApplyResponse(result)),
            ApplyProductFiscalProfileImportBatchOutcome.NotFound => TypedResults.NotFound(),
            _ => TypedResults.BadRequest(MapApplyResponse(result))
        };
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private static ImportBatchSummaryResponse MapBatchSummary(FiscalReceiverImportBatch? batch, string? errorMessage = null)
    {
        return new ImportBatchSummaryResponse
        {
            BatchId = batch?.Id,
            SourceFileName = batch?.SourceFileName ?? string.Empty,
            Status = batch?.Status.ToString() ?? string.Empty,
            TotalRows = batch?.TotalRows ?? 0,
            ValidRows = batch?.ValidRows ?? 0,
            InvalidRows = batch?.InvalidRows ?? 0,
            IgnoredRows = batch?.IgnoredRows ?? 0,
            ExistingMasterMatches = batch?.ExistingMasterMatches ?? 0,
            DuplicateRowsInFile = batch?.DuplicateRowsInFile ?? 0,
            AppliedRows = batch?.AppliedRows ?? 0,
            ApplyFailedRows = batch?.ApplyFailedRows ?? 0,
            ApplySkippedRows = batch?.ApplySkippedRows ?? 0,
            CompletedAtUtc = batch?.CompletedAtUtc,
            LastAppliedAtUtc = batch?.LastAppliedAtUtc,
            ErrorMessage = errorMessage
        };
    }

    private static ImportBatchSummaryResponse MapBatchSummary(ProductFiscalProfileImportBatch? batch, string? errorMessage = null)
    {
        return new ImportBatchSummaryResponse
        {
            BatchId = batch?.Id,
            SourceFileName = batch?.SourceFileName ?? string.Empty,
            Status = batch?.Status.ToString() ?? string.Empty,
            TotalRows = batch?.TotalRows ?? 0,
            ValidRows = batch?.ValidRows ?? 0,
            InvalidRows = batch?.InvalidRows ?? 0,
            IgnoredRows = batch?.IgnoredRows ?? 0,
            ExistingMasterMatches = batch?.ExistingMasterMatches ?? 0,
            DuplicateRowsInFile = batch?.DuplicateRowsInFile ?? 0,
            AppliedRows = batch?.AppliedRows ?? 0,
            ApplyFailedRows = batch?.ApplyFailedRows ?? 0,
            ApplySkippedRows = batch?.ApplySkippedRows ?? 0,
            CompletedAtUtc = batch?.CompletedAtUtc,
            LastAppliedAtUtc = batch?.LastAppliedAtUtc,
            ErrorMessage = errorMessage
        };
    }

    private static FiscalReceiverImportRowResponse MapReceiverRow(FiscalReceiverImportRow row)
    {
        return new FiscalReceiverImportRowResponse
        {
            RowNumber = row.RowNumber,
            Status = row.Status.ToString(),
            SuggestedAction = row.SuggestedAction.ToString(),
            NormalizedRfc = row.NormalizedRfc,
            NormalizedLegalName = row.NormalizedLegalName,
            NormalizedCfdiUseCodeDefault = row.NormalizedCfdiUseCodeDefault,
            NormalizedFiscalRegimeCode = row.NormalizedFiscalRegimeCode,
            NormalizedPostalCode = row.NormalizedPostalCode,
            ValidationErrors = DeserializeErrors(row.ValidationErrors),
            ExistingMasterEntityId = row.ExistingFiscalReceiverId,
            ApplyStatus = row.ApplyStatus.ToString(),
            AppliedAtUtc = row.AppliedAtUtc,
            ApplyErrorMessage = row.ApplyErrorMessage,
            AppliedMasterEntityId = row.AppliedMasterEntityId
        };
    }

    private static ProductFiscalProfileImportRowResponse MapProductRow(ProductFiscalProfileImportRow row)
    {
        return new ProductFiscalProfileImportRowResponse
        {
            RowNumber = row.RowNumber,
            Status = row.Status.ToString(),
            SuggestedAction = row.SuggestedAction.ToString(),
            NormalizedInternalCode = row.NormalizedInternalCode,
            NormalizedDescription = row.NormalizedDescription,
            NormalizedSatProductServiceCode = row.NormalizedSatProductServiceCode,
            NormalizedSatUnitCode = row.NormalizedSatUnitCode,
            NormalizedTaxObjectCode = row.NormalizedTaxObjectCode,
            NormalizedVatRate = row.NormalizedVatRate,
            ValidationErrors = DeserializeErrors(row.ValidationErrors),
            ExistingMasterEntityId = row.ExistingProductFiscalProfileId,
            ApplyStatus = row.ApplyStatus.ToString(),
            AppliedAtUtc = row.AppliedAtUtc,
            ApplyErrorMessage = row.ApplyErrorMessage,
            AppliedMasterEntityId = row.AppliedMasterEntityId
        };
    }

    private static ApplyImportBatchResponse MapApplyResponse(ApplyFiscalReceiverImportBatchResult result)
    {
        return new ApplyImportBatchResponse
        {
            BatchId = result.BatchId,
            ApplyMode = result.ApplyMode.ToString(),
            TotalCandidateRows = result.TotalCandidateRows,
            AppliedRows = result.AppliedRows,
            SkippedRows = result.SkippedRows,
            FailedRows = result.FailedRows,
            AlreadyAppliedRows = result.AlreadyAppliedRows,
            LastAppliedAtUtc = result.LastAppliedAtUtc,
            ErrorMessage = result.ErrorMessage,
            Rows = result.Rows.Select(row => new ApplyImportBatchRowResponse
            {
                RowNumber = row.RowNumber,
                EffectiveAction = row.EffectiveAction,
                ApplyStatus = row.ApplyStatus.ToString(),
                AppliedMasterEntityId = row.AppliedMasterEntityId,
                ErrorMessage = row.ErrorMessage
            }).ToList()
        };
    }

    private static ApplyImportBatchResponse MapApplyResponse(ApplyProductFiscalProfileImportBatchResult result)
    {
        return new ApplyImportBatchResponse
        {
            BatchId = result.BatchId,
            ApplyMode = result.ApplyMode.ToString(),
            TotalCandidateRows = result.TotalCandidateRows,
            AppliedRows = result.AppliedRows,
            SkippedRows = result.SkippedRows,
            FailedRows = result.FailedRows,
            AlreadyAppliedRows = result.AlreadyAppliedRows,
            LastAppliedAtUtc = result.LastAppliedAtUtc,
            ErrorMessage = result.ErrorMessage,
            Rows = result.Rows.Select(row => new ApplyImportBatchRowResponse
            {
                RowNumber = row.RowNumber,
                EffectiveAction = row.EffectiveAction,
                ApplyStatus = row.ApplyStatus.ToString(),
                AppliedMasterEntityId = row.AppliedMasterEntityId,
                ErrorMessage = row.ErrorMessage
            }).ToList()
        };
    }

    private static IReadOnlyList<string> DeserializeErrors(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    public sealed class PreviewProductFiscalProfileImportRequest
    {
        public IFormFile File { get; init; } = default!;
        public string? DefaultTaxObjectCode { get; init; }
        public decimal? DefaultVatRate { get; init; }
        public string? DefaultUnitText { get; init; }
    }

    public sealed class ImportBatchSummaryResponse
    {
        public long? BatchId { get; init; }
        public string SourceFileName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int TotalRows { get; init; }
        public int ValidRows { get; init; }
        public int InvalidRows { get; init; }
        public int IgnoredRows { get; init; }
        public int ExistingMasterMatches { get; init; }
        public int DuplicateRowsInFile { get; init; }
        public int AppliedRows { get; init; }
        public int ApplyFailedRows { get; init; }
        public int ApplySkippedRows { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
        public DateTime? LastAppliedAtUtc { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed class FiscalReceiverImportRowResponse
    {
        public int RowNumber { get; init; }
        public string Status { get; init; } = string.Empty;
        public string SuggestedAction { get; init; } = string.Empty;
        public string? NormalizedRfc { get; init; }
        public string? NormalizedLegalName { get; init; }
        public string? NormalizedCfdiUseCodeDefault { get; init; }
        public string? NormalizedFiscalRegimeCode { get; init; }
        public string? NormalizedPostalCode { get; init; }
        public IReadOnlyList<string> ValidationErrors { get; init; } = [];
        public long? ExistingMasterEntityId { get; init; }
        public string ApplyStatus { get; init; } = string.Empty;
        public DateTime? AppliedAtUtc { get; init; }
        public string? ApplyErrorMessage { get; init; }
        public long? AppliedMasterEntityId { get; init; }
    }

    public sealed class ProductFiscalProfileImportRowResponse
    {
        public int RowNumber { get; init; }
        public string Status { get; init; } = string.Empty;
        public string SuggestedAction { get; init; } = string.Empty;
        public string? NormalizedInternalCode { get; init; }
        public string? NormalizedDescription { get; init; }
        public string? NormalizedSatProductServiceCode { get; init; }
        public string? NormalizedSatUnitCode { get; init; }
        public string? NormalizedTaxObjectCode { get; init; }
        public decimal? NormalizedVatRate { get; init; }
        public IReadOnlyList<string> ValidationErrors { get; init; } = [];
        public long? ExistingMasterEntityId { get; init; }
        public string ApplyStatus { get; init; } = string.Empty;
        public DateTime? AppliedAtUtc { get; init; }
        public string? ApplyErrorMessage { get; init; }
        public long? AppliedMasterEntityId { get; init; }
    }

    public sealed class ApplyImportBatchRequest
    {
        public Pineda.Facturacion.Application.Common.ImportApplyMode ApplyMode { get; init; }
        public IReadOnlyList<int>? SelectedRowNumbers { get; init; }
        public bool StopOnFirstError { get; init; }
    }

    public sealed class ApplyImportBatchResponse
    {
        public long BatchId { get; init; }
        public string ApplyMode { get; init; } = string.Empty;
        public int TotalCandidateRows { get; init; }
        public int AppliedRows { get; init; }
        public int SkippedRows { get; init; }
        public int FailedRows { get; init; }
        public int AlreadyAppliedRows { get; init; }
        public DateTime? LastAppliedAtUtc { get; init; }
        public string? ErrorMessage { get; init; }
        public IReadOnlyList<ApplyImportBatchRowResponse> Rows { get; init; } = [];
    }

    public sealed class ApplyImportBatchRowResponse
    {
        public int RowNumber { get; init; }
        public string EffectiveAction { get; init; } = string.Empty;
        public string ApplyStatus { get; init; } = string.Empty;
        public long? AppliedMasterEntityId { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
