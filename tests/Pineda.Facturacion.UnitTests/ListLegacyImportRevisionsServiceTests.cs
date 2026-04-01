using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class ListLegacyImportRevisionsServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SynthesizesCompatibilityRevision_WhenNoPersistedRevisionsExist()
    {
        var service = new ListLegacyImportRevisionsService(
            new FakeLegacyImportRecordRepository(new LegacyImportRecord
            {
                Id = 10,
                SourceSystem = "legacy",
                SourceTable = "pedidos",
                SourceDocumentId = "LEG-1001",
                SourceHash = "hash-1",
                ImportStatus = ImportStatus.Imported,
                ImportedAtUtc = new DateTime(2026, 04, 01, 12, 0, 0, DateTimeKind.Utc)
            }),
            new FakeLegacyImportRevisionRepository(),
            new FakeImportedLegacyOrderLookupRepository(new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = "LEG-1001",
                SalesOrderId = 20,
                BillingDocumentId = 30,
                FiscalDocumentId = 40
            }));

        var result = await service.ExecuteAsync("LEG-1001");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.CurrentRevisionNumber);
        var revision = Assert.Single(result.Revisions);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.True(revision.IsCurrent);
        Assert.Equal("Imported", revision.ActionType);
        Assert.Equal("NotAvailableYet", revision.EligibilityStatus);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPersistedRevisionsOrderedByRevisionNumberDesc()
    {
        var service = new ListLegacyImportRevisionsService(
            new FakeLegacyImportRecordRepository(new LegacyImportRecord
            {
                Id = 10,
                SourceSystem = "legacy",
                SourceTable = "pedidos",
                SourceDocumentId = "LEG-1001",
                SourceHash = "hash-2",
                ImportStatus = ImportStatus.Imported
            }),
            new FakeLegacyImportRevisionRepository(
            [
                new LegacyImportRevision
                {
                    Id = 1,
                    LegacyImportRecordId = 10,
                    LegacyOrderId = "LEG-1001",
                    RevisionNumber = 1,
                    ActionType = "Imported",
                    Outcome = "Imported",
                    SourceHash = "hash-1",
                    AppliedAtUtc = new DateTime(2026, 04, 01, 12, 0, 0, DateTimeKind.Utc),
                    IsCurrent = false
                },
                new LegacyImportRevision
                {
                    Id = 2,
                    LegacyImportRecordId = 10,
                    LegacyOrderId = "LEG-1001",
                    RevisionNumber = 2,
                    PreviousRevisionNumber = 1,
                    ActionType = "Reimported",
                    Outcome = "Reimported",
                    SourceHash = "hash-2",
                    PreviousSourceHash = "hash-1",
                    AppliedAtUtc = new DateTime(2026, 04, 02, 12, 0, 0, DateTimeKind.Utc),
                    IsCurrent = true
                }
            ]),
            new FakeImportedLegacyOrderLookupRepository());

        var result = await service.ExecuteAsync("LEG-1001");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.CurrentRevisionNumber);
        Assert.Equal([2, 1], result.Revisions.Select(x => x.RevisionNumber).ToArray());
        Assert.True(result.Revisions[0].IsCurrent);
        Assert.Equal("Reimported", result.Revisions[0].ActionType);
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        private readonly LegacyImportRecord? _record;

        public FakeLegacyImportRecordRepository(LegacyImportRecord? record)
        {
            _record = record;
        }

        public Task<LegacyImportRecord?> GetByIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(_record);

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(string sourceSystem, string sourceTable, string sourceDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(_record);

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLegacyImportRevisionRepository : ILegacyImportRevisionRepository
    {
        private readonly List<LegacyImportRevision> _revisions;

        public FakeLegacyImportRevisionRepository(IEnumerable<LegacyImportRevision>? revisions = null)
        {
            _revisions = revisions?.OrderByDescending(x => x.RevisionNumber).ToList() ?? [];
        }

        public Task<LegacyImportRevision?> GetCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(_revisions.FirstOrDefault(x => x.LegacyImportRecordId == legacyImportRecordId && x.IsCurrent));

        public Task<IReadOnlyList<LegacyImportRevision>> ListByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LegacyImportRevision>>(_revisions.Where(x => x.LegacyImportRecordId == legacyImportRecordId).ToArray());

        public Task<int> GetNextRevisionNumberAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(_revisions.Where(x => x.LegacyImportRecordId == legacyImportRecordId).Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1);

        public Task AddAsync(LegacyImportRevision revision, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeImportedLegacyOrderLookupRepository : IImportedLegacyOrderLookupRepository
    {
        private readonly IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel> _results;

        public FakeImportedLegacyOrderLookupRepository(params ImportedLegacyOrderLookupModel[] results)
        {
            _results = results.ToDictionary(x => x.LegacyOrderId, x => x, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>> GetByLegacyOrderIdsAsync(IReadOnlyCollection<string> legacyOrderIds, CancellationToken cancellationToken = default)
        {
            var matched = _results
                .Where(x => legacyOrderIds.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>>(matched);
        }
    }
}
