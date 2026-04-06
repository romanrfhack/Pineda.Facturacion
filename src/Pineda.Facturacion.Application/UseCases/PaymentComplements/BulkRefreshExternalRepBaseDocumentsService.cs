namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class BulkRefreshExternalRepBaseDocumentsService
{
    public const int MaxDocumentsPerRequest = 50;

    private readonly SearchExternalRepBaseDocumentsService _searchService;
    private readonly GetExternalRepBaseDocumentByIdService _detailService;
    private readonly RefreshExternalRepBaseDocumentPaymentComplementStatusService _refreshService;

    public BulkRefreshExternalRepBaseDocumentsService(
        SearchExternalRepBaseDocumentsService searchService,
        GetExternalRepBaseDocumentByIdService detailService,
        RefreshExternalRepBaseDocumentPaymentComplementStatusService refreshService)
    {
        _searchService = searchService;
        _detailService = detailService;
        _refreshService = refreshService;
    }

    public virtual async Task<RepBaseDocumentBulkRefreshResult> ExecuteAsync(
        BulkRefreshExternalRepBaseDocumentsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RepBaseDocumentBulkRefreshMode.IsKnown(command.Mode))
        {
            return ValidationFailure(command.Mode, "El modo de refresh masivo no es válido.");
        }

        if (command.FromDate.HasValue && command.ToDate.HasValue && command.FromDate.Value > command.ToDate.Value)
        {
            return ValidationFailure(command.Mode, "La fecha inicial no puede ser mayor a la fecha final.");
        }

        var targetsResult = await ResolveTargetsAsync(command, cancellationToken);
        if (!targetsResult.IsSuccess)
        {
            return targetsResult;
        }

        var items = new List<RepBaseDocumentBulkRefreshItemResult>();

        foreach (var externalRepBaseDocumentId in targetsResult.Items.Select(x => x.SourceId))
        {
            items.Add(await RefreshOneAsync(externalRepBaseDocumentId, cancellationToken));
        }

        return BuildSuccess(command.Mode, targetsResult.TotalRequested, items);
    }

    protected virtual async Task<RepBaseDocumentBulkRefreshItemResult> RefreshOneAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken)
    {
        var beforeDetail = await _detailService.ExecuteAsync(externalRepBaseDocumentId, cancellationToken);
        var previousComplementStatus = beforeDetail.Document?.PaymentComplements
            .FirstOrDefault(IsRefreshableStatus)?.Status;

        var refreshResult = await _refreshService.ExecuteAsync(
            new RefreshExternalRepBaseDocumentPaymentComplementStatusCommand
            {
                ExternalRepBaseDocumentId = externalRepBaseDocumentId
            },
            cancellationToken);

        var blocked = refreshResult.Outcome == RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.Conflict;
        var outcome = RepBaseDocumentBulkRefreshOutcomeEvaluator.Evaluate(
            refreshResult.IsSuccess,
            previousComplementStatus,
            refreshResult.PaymentComplementStatus,
            blocked);

        return new RepBaseDocumentBulkRefreshItemResult
        {
            SourceType = RepBaseDocumentSourceType.External.ToString(),
            SourceId = externalRepBaseDocumentId,
            Attempted = true,
            Outcome = outcome,
            Message = RepBaseDocumentBulkRefreshOutcomeEvaluator.BuildMessage(
                outcome,
                refreshResult.ErrorMessage,
                refreshResult.LastKnownExternalStatus),
            PaymentComplementDocumentId = refreshResult.PaymentComplementDocumentId,
            PaymentComplementStatus = refreshResult.PaymentComplementStatus,
            LastKnownExternalStatus = refreshResult.LastKnownExternalStatus,
            UpdatedState = refreshResult.UpdatedSummary is null
                ? null
                : RepBaseDocumentBulkRefreshStateMapper.Map(refreshResult.UpdatedSummary)
        };
    }

    protected virtual async Task<RepBaseDocumentBulkRefreshResult> ResolveTargetsAsync(
        BulkRefreshExternalRepBaseDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.Equals(command.Mode, RepBaseDocumentBulkRefreshMode.Selected, StringComparison.Ordinal))
        {
            var selectedIds = command.Documents
                .Where(x => x.SourceId > 0)
                .Select(x => x.SourceId)
                .Distinct()
                .ToList();

            if (selectedIds.Count == 0)
            {
                return ValidationFailure(command.Mode, "Debes seleccionar al menos un documento externo para refrescar.");
            }

            if (selectedIds.Count > MaxDocumentsPerRequest)
            {
                return ValidationFailure(command.Mode, $"El límite máximo por operación es {MaxDocumentsPerRequest} documentos.");
            }

            return new RepBaseDocumentBulkRefreshResult
            {
                IsSuccess = true,
                Mode = command.Mode,
                MaxDocuments = MaxDocumentsPerRequest,
                TotalRequested = selectedIds.Count,
                Items = selectedIds
                    .Select(id => new RepBaseDocumentBulkRefreshItemResult
                    {
                        SourceType = RepBaseDocumentSourceType.External.ToString(),
                        SourceId = id
                    })
                    .ToList()
            };
        }

        var searchResult = await _searchService.ExecuteAsync(
            new SearchExternalRepBaseDocumentsFilter
            {
                Page = 1,
                PageSize = MaxDocumentsPerRequest,
                FromDate = command.FromDate,
                ToDate = command.ToDate,
                ReceiverRfc = command.ReceiverRfc,
                Query = command.Query,
                ValidationStatus = command.ValidationStatus,
                Eligible = command.Eligible,
                Blocked = command.Blocked,
                AlertCode = command.AlertCode,
                Severity = command.Severity,
                NextRecommendedAction = command.NextRecommendedAction,
                QuickView = command.QuickView
            },
            cancellationToken);

        if (searchResult.TotalCount == 0)
        {
            return ValidationFailure(command.Mode, "No hay documentos externos en el conjunto filtrado para refrescar.");
        }

        if (searchResult.TotalCount > MaxDocumentsPerRequest)
        {
            return ValidationFailure(command.Mode, $"El conjunto filtrado excede el límite máximo de {MaxDocumentsPerRequest} documentos.");
        }

        return new RepBaseDocumentBulkRefreshResult
        {
            IsSuccess = true,
            Mode = command.Mode,
            MaxDocuments = MaxDocumentsPerRequest,
            TotalRequested = searchResult.TotalCount,
            Items = searchResult.Items
                .Select(x => new RepBaseDocumentBulkRefreshItemResult
                {
                    SourceType = RepBaseDocumentSourceType.External.ToString(),
                    SourceId = x.ExternalRepBaseDocumentId
                })
                .ToList()
        };
    }

    private static bool IsRefreshableStatus(ExternalRepBaseDocumentPaymentComplementReadModel complement)
    {
        return complement.Status is "Stamped" or "CancellationRequested" or "CancellationRejected" or "Cancelled";
    }

    private static RepBaseDocumentBulkRefreshResult BuildSuccess(
        string mode,
        int totalRequested,
        IReadOnlyList<RepBaseDocumentBulkRefreshItemResult> items)
    {
        return new RepBaseDocumentBulkRefreshResult
        {
            IsSuccess = true,
            Mode = mode,
            MaxDocuments = MaxDocumentsPerRequest,
            TotalRequested = totalRequested,
            TotalAttempted = items.Count(x => x.Attempted),
            RefreshedCount = items.Count(x => x.Outcome == RepBaseDocumentBulkRefreshItemOutcome.Refreshed),
            NoChangesCount = items.Count(x => x.Outcome == RepBaseDocumentBulkRefreshItemOutcome.NoChanges),
            BlockedCount = items.Count(x => x.Outcome == RepBaseDocumentBulkRefreshItemOutcome.Blocked),
            FailedCount = items.Count(x => x.Outcome == RepBaseDocumentBulkRefreshItemOutcome.Failed),
            Items = items
        };
    }

    private static RepBaseDocumentBulkRefreshResult ValidationFailure(string mode, string errorMessage)
    {
        return new RepBaseDocumentBulkRefreshResult
        {
            IsSuccess = false,
            Mode = mode,
            MaxDocuments = MaxDocumentsPerRequest,
            ErrorMessage = errorMessage
        };
    }
}
