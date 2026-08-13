from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8-sig")


def write(path: str, content: str) -> None:
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    if old not in text:
        raise RuntimeError(f"Expected snippet not found in {path}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


write(
    "src/Pineda.Facturacion.Application/Abstractions/Legacy/ILegacyOrderPaymentReader.cs",
    '''namespace Pineda.Facturacion.Application.Abstractions.Legacy;

public interface ILegacyOrderPaymentReader
{
    Task<LegacyOrderPaymentReadModel?> GetByOrderIdAsync(
        string legacyOrderId,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyOrderPaymentReadModel
{
    public string? LegacyPaymentCode { get; init; }

    public string? LegacyPaymentDescription { get; init; }
}
''')

write(
    "src/Pineda.Facturacion.Infrastructure.LegacyRead/Readers/LegacyOrderPaymentReader.cs",
    '''using System.Data;
using MySqlConnector;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Infrastructure.LegacyRead.Options;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

public sealed class LegacyOrderPaymentReader : ILegacyOrderPaymentReader
{
    private readonly string _connectionString;
    private readonly LegacySchemaResolver _schemaResolver = new();

    public LegacyOrderPaymentReader(LegacyReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Legacy read connection string is required.", nameof(options));
        }

        _connectionString = options.ConnectionString;
    }

    public async Task<LegacyOrderPaymentReadModel?> GetByOrderIdAsync(
        string legacyOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyOrderId))
        {
            return null;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var orders = await ResolveTableAsync(
            connection,
            "pedidos",
            ["noPedido", "cveVendedor"],
            cancellationToken);
        var vendors = await ResolveTableAsync(
            connection,
            "vendedores",
            ["cveVendedor", "Vendedor"],
            cancellationToken);

        await using var command = new MySqlCommand(BuildSql(orders, vendors), connection);
        command.Parameters.AddWithValue("@legacyOrderId", legacyOrderId.Trim());

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LegacyOrderPaymentReadModel
        {
            LegacyPaymentCode = GetNullableTrimmedString(reader, "LegacyPaymentCode"),
            LegacyPaymentDescription = GetNullableTrimmedString(reader, "LegacyPaymentDescription")
        };
    }

    internal static string BuildSql(ResolvedLegacyTable orders, ResolvedLegacyTable vendors)
    {
        return $"""
            SELECT
                NULLIF(TRIM(p.{Q(orders["cveVendedor"])}), '') AS LegacyPaymentCode,
                NULLIF(TRIM(v.{Q(vendors["Vendedor"])}), '') AS LegacyPaymentDescription
            FROM {Q(orders.ActualName)} p
            LEFT JOIN {Q(vendors.ActualName)} v
                ON v.{Q(vendors["cveVendedor"])} = p.{Q(orders["cveVendedor"])}
            WHERE p.{Q(orders["noPedido"])} = @legacyOrderId
            LIMIT 1;
            """;
    }

    private async Task<ResolvedLegacyTable> ResolveTableAsync(
        MySqlConnection connection,
        string logicalTableName,
        IReadOnlyList<string> requiredColumns,
        CancellationToken cancellationToken)
    {
        var actualTableName = await _schemaResolver.ResolveTableAsync(connection, logicalTableName, cancellationToken);
        var columns = await _schemaResolver.ResolveColumnsAsync(
            connection,
            actualTableName,
            logicalTableName,
            requiredColumns,
            cancellationToken);
        return new ResolvedLegacyTable(logicalTableName, actualTableName, columns);
    }

    private static string? GetNullableTrimmedString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = Convert.ToString(reader.GetValue(ordinal))?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Q(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
''')

write(
    "src/Pineda.Facturacion.Application/Common/LegacyPaymentSuggestionPolicy.cs",
    '''namespace Pineda.Facturacion.Application.Common;

public sealed record LegacyPaymentSuggestion(
    string PaymentMethodSat,
    string PaymentFormSat,
    bool IsCreditSale,
    string SourceDescription);

public static class LegacyPaymentSuggestionPolicy
{
    public static LegacyPaymentSuggestion? Resolve(string? legacyPaymentCode)
    {
        return legacyPaymentCode?.Trim() switch
        {
            "10" => new LegacyPaymentSuggestion("PUE", "01", false, "Efectivo"),
            "20" => new LegacyPaymentSuggestion("PUE", "04", false, "TDC"),
            "30" => new LegacyPaymentSuggestion("PUE", "28", false, "TDD"),
            "40" => new LegacyPaymentSuggestion("PUE", "03", false, "Transferencia"),
            "50" => new LegacyPaymentSuggestion("PPD", "99", true, "Crédito"),
            _ => null
        };
    }

    public static LegacyPaymentSuggestion? ResolveCommon(IEnumerable<string?> legacyPaymentCodes)
    {
        var codes = legacyPaymentCodes.ToArray();
        if (codes.Length == 0)
        {
            return null;
        }

        var suggestions = codes.Select(Resolve).ToArray();
        if (suggestions.Any(x => x is null))
        {
            return null;
        }

        var first = suggestions[0]!;
        return suggestions.All(x =>
                string.Equals(x!.PaymentMethodSat, first.PaymentMethodSat, StringComparison.Ordinal)
                && string.Equals(x.PaymentFormSat, first.PaymentFormSat, StringComparison.Ordinal)
                && x.IsCreditSale == first.IsCreditSale)
            ? first
            : null;
    }
}
''')

write(
    "src/Pineda.Facturacion.Application/UseCases/BillingDocuments/GetBillingDocumentLegacyPaymentSuggestionService.cs",
    '''using Microsoft.Extensions.Logging;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class GetBillingDocumentLegacyPaymentSuggestionService
{
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderPaymentReader _legacyOrderPaymentReader;
    private readonly ILogger<GetBillingDocumentLegacyPaymentSuggestionService> _logger;

    public GetBillingDocumentLegacyPaymentSuggestionService(
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        ILegacyOrderPaymentReader legacyOrderPaymentReader,
        ILogger<GetBillingDocumentLegacyPaymentSuggestionService> logger)
    {
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _legacyOrderPaymentReader = legacyOrderPaymentReader;
        _logger = logger;
    }

    public async Task<BillingDocumentLegacyPaymentSuggestionResult> ExecuteAsync(
        long billingDocumentId,
        CancellationToken cancellationToken = default)
    {
        if (billingDocumentId <= 0)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(0);
        }

        var salesOrders = await _salesOrderSnapshotRepository.GetByBillingDocumentIdWithItemsAsync(
            billingDocumentId,
            cancellationToken);
        if (salesOrders.Count == 0)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(0);
        }

        var paymentCodes = new List<string?>(salesOrders.Count);
        foreach (var salesOrder in salesOrders)
        {
            var paymentCode = salesOrder.LegacyPaymentCode;
            if (string.IsNullOrWhiteSpace(paymentCode))
            {
                var importRecord = await _legacyImportRecordRepository.GetByIdAsync(
                    salesOrder.LegacyImportRecordId,
                    cancellationToken);
                if (importRecord is not null && !string.IsNullOrWhiteSpace(importRecord.SourceDocumentId))
                {
                    try
                    {
                        var livePayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                            importRecord.SourceDocumentId,
                            cancellationToken);
                        paymentCode = livePayment?.LegacyPaymentCode;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Legacy payment suggestion lookup failed for BillingDocumentId={BillingDocumentId} LegacyOrderId={LegacyOrderId}. Manual fiscal capture remains available.",
                            billingDocumentId,
                            importRecord.SourceDocumentId);
                        return BillingDocumentLegacyPaymentSuggestionResult.Unavailable(salesOrders.Count);
                    }
                }
            }

            paymentCodes.Add(paymentCode);
        }

        var individualSuggestions = paymentCodes
            .Select(LegacyPaymentSuggestionPolicy.Resolve)
            .ToArray();
        if (individualSuggestions.Any(x => x is null))
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Unknown(salesOrders.Count);
        }

        var commonSuggestion = LegacyPaymentSuggestionPolicy.ResolveCommon(paymentCodes);
        if (commonSuggestion is null)
        {
            return BillingDocumentLegacyPaymentSuggestionResult.Mixed(salesOrders.Count);
        }

        return BillingDocumentLegacyPaymentSuggestionResult.Suggested(
            salesOrders.Count,
            commonSuggestion);
    }
}

public static class LegacyPaymentSuggestionStatuses
{
    public const string Suggested = "Suggested";
    public const string Mixed = "Mixed";
    public const string Unknown = "Unknown";
    public const string Unavailable = "Unavailable";
}

public sealed class BillingDocumentLegacyPaymentSuggestionResult
{
    public string Status { get; init; } = LegacyPaymentSuggestionStatuses.Unavailable;

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public bool? IsCreditSale { get; init; }

    public string? SourceDescription { get; init; }

    public int SourceOrderCount { get; init; }

    public static BillingDocumentLegacyPaymentSuggestionResult Suggested(
        int sourceOrderCount,
        LegacyPaymentSuggestion suggestion)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Suggested,
            PaymentMethodSat = suggestion.PaymentMethodSat,
            PaymentFormSat = suggestion.PaymentFormSat,
            IsCreditSale = suggestion.IsCreditSale,
            SourceDescription = suggestion.SourceDescription,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Mixed(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Mixed,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Unknown(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Unknown,
            SourceOrderCount = sourceOrderCount
        };
    }

    public static BillingDocumentLegacyPaymentSuggestionResult Unavailable(int sourceOrderCount)
    {
        return new BillingDocumentLegacyPaymentSuggestionResult
        {
            Status = LegacyPaymentSuggestionStatuses.Unavailable,
            SourceOrderCount = sourceOrderCount
        };
    }
}
''')

write(
    "src/Pineda.Facturacion.Api/Endpoints/BillingDocumentPaymentSuggestionsEndpoints.cs",
    '''using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;

namespace Pineda.Facturacion.Api.Endpoints;

public static class BillingDocumentPaymentSuggestionsEndpoints
{
    public static IEndpointRouteBuilder MapBillingDocumentPaymentSuggestionsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/billing-documents")
            .WithTags("BillingDocuments")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapGet("/{billingDocumentId:long}/payment-suggestion", GetPaymentSuggestionAsync)
            .WithName("GetBillingDocumentLegacyPaymentSuggestion")
            .WithSummary("Get an advisory SAT payment prefill derived from recognized legacy order payment codes")
            .Produces<BillingDocumentLegacyPaymentSuggestionResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<Ok<BillingDocumentLegacyPaymentSuggestionResponse>> GetPaymentSuggestionAsync(
        long billingDocumentId,
        GetBillingDocumentLegacyPaymentSuggestionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(billingDocumentId, cancellationToken);
        return TypedResults.Ok(new BillingDocumentLegacyPaymentSuggestionResponse
        {
            Status = result.Status,
            PaymentMethodSat = result.PaymentMethodSat,
            PaymentFormSat = result.PaymentFormSat,
            IsCreditSale = result.IsCreditSale,
            SourceDescription = result.SourceDescription,
            SourceOrderCount = result.SourceOrderCount
        });
    }
}

public sealed class BillingDocumentLegacyPaymentSuggestionResponse
{
    public string Status { get; init; } = LegacyPaymentSuggestionStatuses.Unavailable;

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public bool? IsCreditSale { get; init; }

    public string? SourceDescription { get; init; }

    public int SourceOrderCount { get; init; }
}
''')

write(
    "tests/Pineda.Facturacion.UnitTests/LegacyPaymentSuggestionPolicyTests.cs",
    '''using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.UnitTests;

public class LegacyPaymentSuggestionPolicyTests
{
    [Theory]
    [InlineData("10", "PUE", "01", false, "Efectivo")]
    [InlineData("20", "PUE", "04", false, "TDC")]
    [InlineData("30", "PUE", "28", false, "TDD")]
    [InlineData("40", "PUE", "03", false, "Transferencia")]
    [InlineData("50", "PPD", "99", true, "Crédito")]
    public void Resolve_Maps_Recognized_Legacy_Payment_Codes(
        string legacyCode,
        string expectedMethod,
        string expectedForm,
        bool expectedCredit,
        string expectedDescription)
    {
        var result = LegacyPaymentSuggestionPolicy.Resolve(legacyCode);

        Assert.NotNull(result);
        Assert.Equal(expectedMethod, result.PaymentMethodSat);
        Assert.Equal(expectedForm, result.PaymentFormSat);
        Assert.Equal(expectedCredit, result.IsCreditSale);
        Assert.Equal(expectedDescription, result.SourceDescription);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("666")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_Does_Not_Guess_Unknown_Legacy_Codes(string? legacyCode)
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.Resolve(legacyCode));
    }

    [Fact]
    public void ResolveCommon_Returns_Suggestion_When_All_Orders_Agree()
    {
        var result = LegacyPaymentSuggestionPolicy.ResolveCommon(["40", "40", "40"]);

        Assert.NotNull(result);
        Assert.Equal("PUE", result.PaymentMethodSat);
        Assert.Equal("03", result.PaymentFormSat);
        Assert.False(result.IsCreditSale);
    }

    [Fact]
    public void ResolveCommon_Returns_Null_When_Recognized_Orders_Disagree()
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.ResolveCommon(["10", "30"]));
    }

    [Fact]
    public void ResolveCommon_Returns_Null_When_Any_Order_Is_Unknown()
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.ResolveCommon(["40", "4"]));
    }
}
''')

write(
    "tests/Pineda.Facturacion.UnitTests/LegacyPaymentHashCompatibilityTests.cs",
    '''using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Infrastructure.Hashing;

namespace Pineda.Facturacion.UnitTests;

public class LegacyPaymentHashCompatibilityTests
{
    [Fact]
    public void GenerateHash_Does_Not_Include_Advisory_Legacy_Payment_Evidence()
    {
        var generator = new Sha256ContentHashGenerator();
        var order = new LegacyOrderReadModel
        {
            LegacyOrderId = "1185568",
            LegacyOrderNumber = "A208345",
            CustomerLegacyId = "1",
            CustomerName = "MOSTRADOR",
            PaymentCondition = "O",
            CurrencyCode = "MXN",
            Total = 100m
        };

        var before = generator.GenerateHash(order);
        order.LegacyPaymentCode = "40";
        order.LegacyPaymentDescription = "Transferencia";
        var after = generator.GenerateHash(order);

        Assert.Equal(before, after);
    }
}
''')

replace_once(
    "src/Pineda.Facturacion.Application/Models/Legacy/LegacyOrderReadModel.cs",
    "    public string PaymentCondition { get; set; } = string.Empty;\n\n    public string? PriceListCode",
    "    public string PaymentCondition { get; set; } = string.Empty;\n\n    public string? LegacyPaymentCode { get; set; }\n\n    public string? LegacyPaymentDescription { get; set; }\n\n    public string? PriceListCode")

replace_once(
    "src/Pineda.Facturacion.Domain/Entities/SalesOrder.cs",
    "    public string PaymentCondition { get; set; } = string.Empty;\n\n    public string? PriceListCode",
    "    public string PaymentCondition { get; set; } = string.Empty;\n\n    public string? LegacyPaymentCode { get; set; }\n\n    public string? LegacyPaymentDescription { get; set; }\n\n    public string? PriceListCode")

replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/LegacyOrderSnapshotMapper.cs",
    "            PaymentCondition = legacyOrder.PaymentCondition,\n            PriceListCode = legacyOrder.PriceListCode,",
    "            PaymentCondition = legacyOrder.PaymentCondition,\n            LegacyPaymentCode = legacyOrder.LegacyPaymentCode,\n            LegacyPaymentDescription = legacyOrder.LegacyPaymentDescription,\n            PriceListCode = legacyOrder.PriceListCode,")

replace_once(
    "src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/SalesOrderConfiguration.cs",
    "        builder.Property(x => x.PaymentCondition).HasMaxLength(50).IsRequired();\n        builder.Property(x => x.PriceListCode).HasMaxLength(50);",
    "        builder.Property(x => x.PaymentCondition).HasMaxLength(50).IsRequired();\n        builder.Property(x => x.LegacyPaymentCode).HasMaxLength(15);\n        builder.Property(x => x.LegacyPaymentDescription).HasMaxLength(60);\n        builder.Property(x => x.PriceListCode).HasMaxLength(50);")

replace_once(
    "src/Pineda.Facturacion.Infrastructure.LegacyRead/DependencyInjection/ServiceCollectionExtensions.cs",
    '''        services.AddScoped<ILegacyOrderReader>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LegacyReadOptions>>().Value;
            return new LegacyOrderReader(options);
        });

        return services;''',
    '''        services.AddScoped<ILegacyOrderReader>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LegacyReadOptions>>().Value;
            return new LegacyOrderReader(options);
        });
        services.AddScoped<ILegacyOrderPaymentReader>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LegacyReadOptions>>().Value;
            return new LegacyOrderPaymentReader(options);
        });

        return services;''')

replace_once(
    "src/Pineda.Facturacion.Application/DependencyInjection/ServiceCollectionExtensions.cs",
    "        services.AddScoped<GetBillingDocumentLookupByIdService>();\n        services.AddScoped<SearchBillingDocumentsService>();",
    "        services.AddScoped<GetBillingDocumentLookupByIdService>();\n        services.AddScoped<GetBillingDocumentLegacyPaymentSuggestionService>();\n        services.AddScoped<SearchBillingDocumentsService>();")

replace_once(
    "src/Pineda.Facturacion.Api/Program.cs",
    "app.MapBillingDocumentsEndpoints();\napp.MapIssuerProfileEndpoints();",
    "app.MapBillingDocumentsEndpoints();\napp.MapBillingDocumentPaymentSuggestionsEndpoints();\napp.MapIssuerProfileEndpoints();")

replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ImportLegacyOrderService.cs",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly ILegacyOrderPaymentReader? _legacyOrderPaymentReader;\n    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;")
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ImportLegacyOrderService.cs",
    '''        IUnitOfWork unitOfWork,
        IContentHashGenerator contentHashGenerator,
        LegacyImportRevisionRecorder legacyImportRevisionRecorder)''',
    '''        IUnitOfWork unitOfWork,
        IContentHashGenerator contentHashGenerator,
        LegacyImportRevisionRecorder legacyImportRevisionRecorder,
        ILegacyOrderPaymentReader? legacyOrderPaymentReader = null)''')
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ImportLegacyOrderService.cs",
    "        _contentHashGenerator = contentHashGenerator;\n        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;",
    "        _contentHashGenerator = contentHashGenerator;\n        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;\n        _legacyOrderPaymentReader = legacyOrderPaymentReader;")
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ImportLegacyOrderService.cs",
    '''        var sourceHash = _contentHashGenerator.GenerateHash(legacyOrder);
        var existingImportRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''',
    '''        var sourceHash = _contentHashGenerator.GenerateHash(legacyOrder);
        if (_legacyOrderPaymentReader is not null)
        {
            var legacyPayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                legacyOrder.LegacyOrderId,
                cancellationToken);
            legacyOrder.LegacyPaymentCode = legacyPayment?.LegacyPaymentCode;
            legacyOrder.LegacyPaymentDescription = legacyPayment?.LegacyPaymentDescription;
        }

        var existingImportRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''')

replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly IContentHashGenerator _contentHashGenerator;",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly ILegacyOrderPaymentReader? _legacyOrderPaymentReader;\n    private readonly IContentHashGenerator _contentHashGenerator;")
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs",
    '''        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)''',
    '''        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork,
        ILegacyOrderPaymentReader? legacyOrderPaymentReader = null)''')
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs",
    "        _productFiscalProfileRepository = productFiscalProfileRepository;\n        _unitOfWork = unitOfWork;",
    "        _productFiscalProfileRepository = productFiscalProfileRepository;\n        _unitOfWork = unitOfWork;\n        _legacyOrderPaymentReader = legacyOrderPaymentReader;")
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs",
    '''        if (!string.Equals(applyHash, preview.CurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Legacy source data changed after the preview. Refresh the preview before retrying reimport.");
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''',
    '''        if (!string.Equals(applyHash, preview.CurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Legacy source data changed after the preview. Refresh the preview before retrying reimport.");
        }

        if (_legacyOrderPaymentReader is not null)
        {
            var legacyPayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                currentLegacyOrder.LegacyOrderId,
                cancellationToken);
            currentLegacyOrder.LegacyPaymentCode = legacyPayment?.LegacyPaymentCode;
            currentLegacyOrder.LegacyPaymentDescription = legacyPayment?.LegacyPaymentDescription;
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''')
replace_once(
    "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs",
    "        salesOrder.PaymentCondition = replacementSnapshot.PaymentCondition;\n        salesOrder.PriceListCode = replacementSnapshot.PriceListCode;",
    "        salesOrder.PaymentCondition = replacementSnapshot.PaymentCondition;\n        salesOrder.LegacyPaymentCode = replacementSnapshot.LegacyPaymentCode;\n        salesOrder.LegacyPaymentDescription = replacementSnapshot.LegacyPaymentDescription;\n        salesOrder.PriceListCode = replacementSnapshot.PriceListCode;")

replace_once(
    "frontend/src/app/features/fiscal-documents/models/fiscal-documents.models.ts",
    "export interface GroupedBillingDocumentSearchResponse {",
    '''export interface BillingDocumentLegacyPaymentSuggestionResponse {
  status: 'Suggested' | 'Mixed' | 'Unknown' | 'Unavailable' | string;
  paymentMethodSat?: string | null;
  paymentFormSat?: string | null;
  isCreditSale?: boolean | null;
  sourceDescription?: string | null;
  sourceOrderCount: number;
}

export interface GroupedBillingDocumentSearchResponse {''')

replace_once(
    "frontend/src/app/features/fiscal-documents/infrastructure/fiscal-documents-api.service.ts",
    "  BillingDocumentLookupResponse,\n  GroupedBillingDocumentSearchResponse,",
    "  BillingDocumentLookupResponse,\n  BillingDocumentLegacyPaymentSuggestionResponse,\n  GroupedBillingDocumentSearchResponse,")
replace_once(
    "frontend/src/app/features/fiscal-documents/infrastructure/fiscal-documents-api.service.ts",
    '''  getBillingDocumentById(billingDocumentId: number): Observable<BillingDocumentLookupResponse> {
    return this.http.get<BillingDocumentLookupResponse>(buildApiUrl(`/billing-documents/${billingDocumentId}`));
  }

  searchBillingDocuments(query: string): Observable<BillingDocumentLookupResponse[]> {''',
    '''  getBillingDocumentById(billingDocumentId: number): Observable<BillingDocumentLookupResponse> {
    return this.http.get<BillingDocumentLookupResponse>(buildApiUrl(`/billing-documents/${billingDocumentId}`));
  }

  getBillingDocumentPaymentSuggestion(
    billingDocumentId: number,
  ): Observable<BillingDocumentLegacyPaymentSuggestionResponse> {
    return this.http.get<BillingDocumentLegacyPaymentSuggestionResponse>(
      buildApiUrl(`/billing-documents/${billingDocumentId}/payment-suggestion`),
      { context: new HttpContext().set(SUPPRESS_GLOBAL_ERROR_TOAST, true) },
    );
  }

  searchBillingDocuments(query: string): Observable<BillingDocumentLookupResponse[]> {''')

component = "frontend/src/app/features/fiscal-documents/pages/fiscal-document-operations-page.component.ts"
replace_once(
    component,
    "  BillingDocumentSearchGroupResponse,\n  BillingDocumentLookupResponse,\n  BillingDocumentLookupItemResponse,",
    "  BillingDocumentSearchGroupResponse,\n  BillingDocumentLookupResponse,\n  BillingDocumentLegacyPaymentSuggestionResponse,\n  BillingDocumentLookupItemResponse,")
replace_once(
    component,
    '''              <small class="helper"
                >Selecciona primero el método SAT para guiar la forma de pago.</small
              >''',
    '''              @if (legacyPaymentSuggestion()?.status === 'Suggested') {
                <small class="helper">
                  Sugerido desde orden Legacy: {{ legacyPaymentSuggestion()?.sourceDescription }}.
                  Puedes modificarlo antes de preparar el CFDI.
                </small>
              } @else if (legacyPaymentSuggestion()?.status === 'Mixed') {
                <small class="helper">
                  Las órdenes asociadas tienen distintas formas de pago registradas en Legacy;
                  selecciona manualmente los datos fiscales.
                </small>
              } @else if (legacyPaymentSuggestion()?.status === 'Unknown') {
                <small class="helper">
                  No se encontró una forma de pago Legacy reconocida y común para todas las órdenes;
                  selecciona manualmente los datos fiscales.
                </small>
              } @else {
                <small class="helper"
                  >Selecciona primero el método SAT para guiar la forma de pago.</small
                >
              }''')
replace_once(
    component,
    "  protected readonly paymentMethodCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);\n  protected readonly paymentFormCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);",
    "  protected readonly paymentMethodCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);\n  protected readonly paymentFormCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);\n  protected readonly legacyPaymentSuggestion =\n    signal<BillingDocumentLegacyPaymentSuggestionResponse | null>(null);")
replace_once(
    component,
    "  private paymentConditionEditedByUser = false;",
    "  private paymentConditionEditedByUser = false;\n  private paymentFieldsEditedByUser = false;")
replace_once(
    component,
    '''  protected onPaymentMethodChange(value: string): void {
    this.paymentMethodSat = normalizeSatCode(value);''',
    '''  protected onPaymentMethodChange(value: string): void {
    this.paymentFieldsEditedByUser = true;
    this.paymentMethodSat = normalizeSatCode(value);''')
replace_once(
    component,
    '''  protected onPaymentFormChange(value: string): void {
    this.paymentFormSat = normalizeSatCode(value);''',
    '''  protected onPaymentFormChange(value: string): void {
    this.paymentFieldsEditedByUser = true;
    this.paymentFormSat = normalizeSatCode(value);''')
replace_once(
    component,
    '''  protected onCreditSaleChange(value: boolean): void {
    this.isCreditSale = value;''',
    '''  protected onCreditSaleChange(value: boolean): void {
    this.paymentFieldsEditedByUser = true;
    this.isCreditSale = value;''')
replace_once(
    component,
    '''    this.selectedPendingBillingRemovalIds.set([]);
    this.resetReceiverSelectionState();
    this.billingDocumentId.set(null);''',
    '''    this.selectedPendingBillingRemovalIds.set([]);
    this.resetReceiverSelectionState();
    this.resetPaymentPreparationState();
    this.billingDocumentId.set(null);''')
replace_once(
    component,
    '''      const isDifferentBillingDocument =
        this.billingDocumentId() !== null &&
        this.billingDocumentId() !== billingDocument.billingDocumentId;

      this.clearMissingProductFiscalProfileState();''',
    '''      const isDifferentBillingDocument =
        this.billingDocumentId() !== null &&
        this.billingDocumentId() !== billingDocument.billingDocumentId;
      const isNewBillingDocument =
        this.billingDocumentContext()?.billingDocumentId !== billingDocument.billingDocumentId;

      this.clearMissingProductFiscalProfileState();''')
replace_once(
    component,
    '''      if (!preserveCurrentFiscalDocument && isDifferentBillingDocument) {
        this.resetReceiverSelectionState();
        this.clearOpenFiscalDocumentState();
        this.blockingCanceledOrders.set([]);
      }

      this.billingDocumentContext.set(billingDocument);''',
    '''      if (!preserveCurrentFiscalDocument && isDifferentBillingDocument) {
        this.resetReceiverSelectionState();
        this.clearOpenFiscalDocumentState();
        this.blockingCanceledOrders.set([]);
      }

      if (!preserveCurrentFiscalDocument && isNewBillingDocument) {
        this.resetPaymentPreparationState();
      }

      this.billingDocumentContext.set(billingDocument);''')
replace_once(
    component,
    '''      this.billingDocumentId.set(billingDocument.billingDocumentId);
      this.billingDocumentQuery = `${billingDocument.billingDocumentId}`;
      await this.loadPendingBillingItems();

      if (syncRoute) {''',
    '''      this.billingDocumentId.set(billingDocument.billingDocumentId);
      this.billingDocumentQuery = `${billingDocument.billingDocumentId}`;
      if (!billingDocument.fiscalDocumentId) {
        await this.loadLegacyPaymentSuggestion(
          billingDocument.billingDocumentId,
          !this.paymentFieldsEditedByUser,
        );
      } else {
        this.legacyPaymentSuggestion.set(null);
      }
      await this.loadPendingBillingItems();

      if (syncRoute) {''')
replace_once(
    component,
    '''  private hasActiveSelectedReceiver(): boolean {
    const receiver = this.selectedReceiver();''',
    '''  private async loadLegacyPaymentSuggestion(
    billingDocumentId: number,
    applyToForm: boolean,
  ): Promise<void> {
    try {
      const suggestion = await firstValueFrom(
        this.api.getBillingDocumentPaymentSuggestion(billingDocumentId),
      );
      this.legacyPaymentSuggestion.set(suggestion);

      if (!applyToForm) {
        return;
      }

      if (
        suggestion.status === 'Suggested' &&
        suggestion.paymentMethodSat &&
        suggestion.paymentFormSat &&
        typeof suggestion.isCreditSale === 'boolean'
      ) {
        this.paymentMethodSat = normalizeSatCode(suggestion.paymentMethodSat);
        this.paymentFormSat = normalizeSatCode(suggestion.paymentFormSat);
        this.isCreditSale = suggestion.isCreditSale;
        this.syncPaymentMethodDependencies(false);
        this.applySuggestedPaymentCondition();
        return;
      }

      this.clearPaymentSuggestionFields();
    } catch {
      this.legacyPaymentSuggestion.set(null);
      if (applyToForm) {
        this.clearPaymentSuggestionFields();
      }
    }
  }

  private resetPaymentPreparationState(): void {
    this.paymentFieldsEditedByUser = false;
    this.paymentConditionEditedByUser = false;
    this.creditDays = 7;
    this.legacyPaymentSuggestion.set(null);
    this.clearPaymentSuggestionFields();
  }

  private clearPaymentSuggestionFields(): void {
    this.paymentMethodSat = '';
    this.paymentFormSat = '';
    this.isCreditSale = false;
    this.applySuggestedPaymentCondition();
  }

  private hasActiveSelectedReceiver(): boolean {
    const receiver = this.selectedReceiver();''')

spec = "frontend/src/app/features/fiscal-documents/pages/fiscal-document-operations-page.component.spec.ts"
replace_once(
    spec,
    '''      searchBillingDocuments: vi.fn().mockReturnValue(of([])),
      searchBillingDocumentsGrouped: vi.fn().mockReturnValue(''',
    '''      searchBillingDocuments: vi.fn().mockReturnValue(of([])),
      getBillingDocumentPaymentSuggestion: vi.fn().mockReturnValue(
        of({
          status: 'Unknown',
          paymentMethodSat: null,
          paymentFormSat: null,
          isCreditSale: null,
          sourceDescription: null,
          sourceOrderCount: 1,
        }),
      ),
      searchBillingDocumentsGrouped: vi.fn().mockReturnValue(''')

print("Implementation patches applied successfully.")
