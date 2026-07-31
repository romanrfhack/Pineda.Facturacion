using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.UnitTests;

public sealed class LegacyOrderStampingGuardTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsCanceledOrders_WithInternalAndLegacyIds()
    {
        var salesOrders = new FakeSalesOrderSnapshotRepository
        {
            Orders =
            [
                new SalesOrder { Id = 21, LegacyImportRecordId = 101, LegacyOrderNumber = "A207329" },
                new SalesOrder { Id = 22, LegacyImportRecordId = 102, LegacyOrderNumber = "A207400" }
            ]
        };
        var legacyReader = new FakeLegacyOrderReader
        {
            CanceledOrderIds = ["1183656"]
        };
        var guard = new LegacyOrderStampingGuard(
            new FakeLegacyImportRecordRepository(
                new LegacyImportRecord { Id = 101, SourceDocumentId = "1183656" },
                new LegacyImportRecord { Id = 102, SourceDocumentId = "1183700" }),
            legacyReader,
            salesOrders);

        var result = await guard.ValidateAsync(30);

        Assert.Equal(LegacyOrderStampingValidationOutcome.BlockedByCanceledOrders, result.Outcome);
        var blockingOrder = Assert.Single(result.BlockingCanceledOrders);
        Assert.Equal(21, blockingOrder.SalesOrderId);
        Assert.Equal("1183656", blockingOrder.LegacyOrderId);
        Assert.Equal(["1183656", "1183700"], legacyReader.LastValidatedOrderIds);
        Assert.Contains("1183656", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_FailsClosed_WhenLegacyValidationThrows()
    {
        var guard = new LegacyOrderStampingGuard(
            new FakeLegacyImportRecordRepository(
                new LegacyImportRecord { Id = 101, SourceDocumentId = "1183656" }),
            new FakeLegacyOrderReader { Exception = new InvalidOperationException("Legacy unavailable") },
            new FakeSalesOrderSnapshotRepository
            {
                Orders = [new SalesOrder { Id = 21, LegacyImportRecordId = 101 }]
            });

        var result = await guard.ValidateAsync(30);

        Assert.Equal(LegacyOrderStampingValidationOutcome.ValidationUnavailable, result.Outcome);
        Assert.Empty(result.BlockingCanceledOrders);
        Assert.Contains("No fue posible validar", result.ErrorMessage, StringComparison.Ordinal);
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        public IReadOnlyList<string> CanceledOrderIds { get; init; } = [];
        public Exception? Exception { get; init; }
        public IReadOnlyList<string> LastValidatedOrderIds { get; private set; } = [];

        public Task<LegacyOrderReadModel?> GetByIdAsync(
            string legacyOrderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyOrderReadModel?>(null);

        public Task<LegacyOrderPageReadModel> SearchAsync(
            LegacyOrderSearchReadModel search,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LegacyOrderPageReadModel());

        public Task<IReadOnlyList<string>> FindCanceledOrderIdsAsync(
            IReadOnlyCollection<string> legacyOrderIds,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            LastValidatedOrderIds = legacyOrderIds.ToArray();
            return Task.FromResult(CanceledOrderIds);
        }
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        private readonly IReadOnlyDictionary<long, LegacyImportRecord> _records;

        public FakeLegacyImportRecordRepository(params LegacyImportRecord[] records)
        {
            _records = records.ToDictionary(x => x.Id);
        }

        public Task<LegacyImportRecord?> GetByIdAsync(
            long legacyImportRecordId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_records.GetValueOrDefault(legacyImportRecordId));

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(
            string sourceSystem,
            string sourceTable,
            string sourceDocumentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyImportRecord?>(null);

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeSalesOrderSnapshotRepository : ISalesOrderSnapshotRepository
    {
        public IReadOnlyList<SalesOrder> Orders { get; init; } = [];

        public Task<SalesOrder?> GetByIdWithItemsAsync(
            long salesOrderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.FirstOrDefault(x => x.Id == salesOrderId));

        public Task<IReadOnlyList<SalesOrder>> GetByBillingDocumentIdWithItemsAsync(
            long billingDocumentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Orders);
    }
}
