using Pineda.Facturacion.Application.UseCases.PaymentComplements;

namespace Pineda.Facturacion.UnitTests;

public class RepBaseDocumentBulkRefreshServicesTests
{
    [Fact]
    public async Task BulkRefreshRepBaseDocuments_Aggregates_Internal_External_And_InvalidSelections()
    {
        var internalService = new StubBulkRefreshInternalRepBaseDocumentsService
        {
            NextResult = new RepBaseDocumentBulkRefreshResult
            {
                IsSuccess = true,
                Mode = RepBaseDocumentBulkRefreshMode.Selected,
                Items =
                [
                    new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = "Internal",
                        SourceId = 101,
                        Attempted = true,
                        Outcome = RepBaseDocumentBulkRefreshItemOutcome.Refreshed,
                        Message = "Estatus refrescado correctamente."
                    }
                ]
            }
        };
        var externalService = new StubBulkRefreshExternalRepBaseDocumentsService
        {
            NextResult = new RepBaseDocumentBulkRefreshResult
            {
                IsSuccess = true,
                Mode = RepBaseDocumentBulkRefreshMode.Selected,
                Items =
                [
                    new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = "External",
                        SourceId = 202,
                        Attempted = true,
                        Outcome = RepBaseDocumentBulkRefreshItemOutcome.Blocked,
                        Message = "El documento no tiene un REP elegible para refresh."
                    }
                ]
            }
        };

        var service = new TestBulkRefreshRepBaseDocumentsService(internalService, externalService)
        {
            ResolveResult = new RepBaseDocumentBulkRefreshResult
            {
                IsSuccess = true,
                Mode = RepBaseDocumentBulkRefreshMode.Selected,
                MaxDocuments = 50,
                TotalRequested = 3,
                Items =
                [
                    new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = "Internal",
                        SourceId = 101,
                        Attempted = true
                    },
                    new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = "Legacy",
                        SourceId = 303,
                        Attempted = false,
                        Outcome = RepBaseDocumentBulkRefreshItemOutcome.Failed,
                        Message = "El origen del documento no es válido para refresh masivo."
                    },
                    new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = "External",
                        SourceId = 202,
                        Attempted = true
                    }
                ]
            }
        };

        var result = await service.ExecuteAsync(new BulkRefreshRepBaseDocumentsCommand
        {
            Mode = RepBaseDocumentBulkRefreshMode.Selected
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.TotalRequested);
        Assert.Equal(2, result.TotalAttempted);
        Assert.Equal(1, result.RefreshedCount);
        Assert.Equal(1, result.BlockedCount);
        Assert.Equal(1, result.FailedCount);

        Assert.Collection(
            result.Items,
            first =>
            {
                Assert.Equal("Internal", first.SourceType);
                Assert.Equal(101, first.SourceId);
                Assert.Equal(RepBaseDocumentBulkRefreshItemOutcome.Refreshed, first.Outcome);
            },
            second =>
            {
                Assert.Equal("Legacy", second.SourceType);
                Assert.Equal(303, second.SourceId);
                Assert.False(second.Attempted);
                Assert.Equal(RepBaseDocumentBulkRefreshItemOutcome.Failed, second.Outcome);
            },
            third =>
            {
                Assert.Equal("External", third.SourceType);
                Assert.Equal(202, third.SourceId);
                Assert.Equal(RepBaseDocumentBulkRefreshItemOutcome.Blocked, third.Outcome);
            });
    }

    [Fact]
    public async Task BulkRefreshRepBaseDocuments_ReturnsValidationFailure_WhenSelectionExceedsLimit()
    {
        var service = new BulkRefreshRepBaseDocumentsService(
            null!,
            new StubBulkRefreshInternalRepBaseDocumentsService(),
            new StubBulkRefreshExternalRepBaseDocumentsService());

        var result = await service.ExecuteAsync(new BulkRefreshRepBaseDocumentsCommand
        {
            Mode = RepBaseDocumentBulkRefreshMode.Selected,
            Documents = Enumerable.Range(1, 51)
                .Select(id => new RepBaseDocumentBulkRefreshDocumentReference
                {
                    SourceType = "Internal",
                    SourceId = id
                })
                .ToList()
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("límite máximo", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestBulkRefreshRepBaseDocumentsService : BulkRefreshRepBaseDocumentsService
    {
        public TestBulkRefreshRepBaseDocumentsService(
            BulkRefreshInternalRepBaseDocumentsService internalService,
            BulkRefreshExternalRepBaseDocumentsService externalService)
            : base(null!, internalService, externalService)
        {
        }

        public RepBaseDocumentBulkRefreshResult ResolveResult { get; set; } = new()
        {
            IsSuccess = true,
            Mode = RepBaseDocumentBulkRefreshMode.Selected
        };

        protected override Task<RepBaseDocumentBulkRefreshResult> ResolveTargetsAsync(
            BulkRefreshRepBaseDocumentsCommand command,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ResolveResult);
        }
    }

    private sealed class StubBulkRefreshInternalRepBaseDocumentsService : BulkRefreshInternalRepBaseDocumentsService
    {
        public StubBulkRefreshInternalRepBaseDocumentsService()
            : base(null!, null!, null!)
        {
        }

        public RepBaseDocumentBulkRefreshResult NextResult { get; set; } = new()
        {
            IsSuccess = true,
            Mode = RepBaseDocumentBulkRefreshMode.Selected
        };

        public override Task<RepBaseDocumentBulkRefreshResult> ExecuteAsync(
            BulkRefreshInternalRepBaseDocumentsCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }
    }

    private sealed class StubBulkRefreshExternalRepBaseDocumentsService : BulkRefreshExternalRepBaseDocumentsService
    {
        public StubBulkRefreshExternalRepBaseDocumentsService()
            : base(null!, null!, null!)
        {
        }

        public RepBaseDocumentBulkRefreshResult NextResult { get; set; } = new()
        {
            IsSuccess = true,
            Mode = RepBaseDocumentBulkRefreshMode.Selected
        };

        public override Task<RepBaseDocumentBulkRefreshResult> ExecuteAsync(
            BulkRefreshExternalRepBaseDocumentsCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }
    }
}
