namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class BulkRefreshInternalRepBaseDocumentsService
{
    public const int MaxDocumentsPerRequest = 50;

    private readonly SearchInternalRepBaseDocumentsService _searchService;
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _detailService;
    private readonly RefreshInternalRepBaseDocumentPaymentComplementStatusService _refreshService;

    public BulkRefreshInternalRepBaseDocumentsService(
        SearchInternalRepBaseDocumentsService searchService,
        GetInternalRepBaseDocumentByFiscalDocumentIdService detailService,
        RefreshInternalRepBaseDocumentPaymentComplementStatusService refreshService)
    {
        _searchService = searchService;
        _detailService = detailService;
        _refreshService = refreshService;
    }

    public virtual async Task<RepBaseDocumentBulkRefreshResult> ExecuteAsync(
        BulkRefreshInternalRepBaseDocumentsCommand command,
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

        foreach (var fiscalDocumentId in targetsResult.Items.Select(x => x.SourceId))
        {
            items.Add(await RefreshOneAsync(fiscalDocumentId, cancellationToken));
        }

        return BuildSuccess(command.Mode, targetsResult.TotalRequested, items);
    }

    protected virtual async Task<RepBaseDocumentBulkRefreshItemResult> RefreshOneAsync(long fiscalDocumentId, CancellationToken cancellationToken)
    {
        var beforeDetail = await _detailService.ExecuteAsync(fiscalDocumentId, cancellationToken);
        var previousComplementStatus = beforeDetail.Document?.PaymentComplements
            .FirstOrDefault(IsRefreshableStatus)?.Status;

        var refreshResult = await _refreshService.ExecuteAsync(
            new RefreshInternalRepBaseDocumentPaymentComplementStatusCommand
            {
                FiscalDocumentId = fiscalDocumentId
            },
            cancellationToken);

        var blocked = refreshResult.Outcome == RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.Conflict;
        var outcome = RepBaseDocumentBulkRefreshOutcomeEvaluator.Evaluate(
            refreshResult.IsSuccess,
            previousComplementStatus,
            refreshResult.PaymentComplementStatus,
            blocked);

        return new RepBaseDocumentBulkRefreshItemResult
        {
            SourceType = RepBaseDocumentSourceType.Internal.ToString(),
            SourceId = fiscalDocumentId,
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
        BulkRefreshInternalRepBaseDocumentsCommand command,
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
                return ValidationFailure(command.Mode, "Debes seleccionar al menos un documento interno para refrescar.");
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
                        SourceType = RepBaseDocumentSourceType.Internal.ToString(),
                        SourceId = id
                    })
                    .ToList()
            };
        }

        var searchResult = await _searchService.ExecuteAsync(
            new SearchInternalRepBaseDocumentsFilter
            {
                Page = 1,
                PageSize = MaxDocumentsPerRequest,
                FromDate = command.FromDate,
                ToDate = command.ToDate,
                ReceiverRfc = command.ReceiverRfc,
                Query = command.Query,
                Eligible = command.Eligible,
                Blocked = command.Blocked,
                WithOutstandingBalance = command.WithOutstandingBalance,
                HasRepEmitted = command.HasRepEmitted,
                AlertCode = command.AlertCode,
                Severity = command.Severity,
                NextRecommendedAction = command.NextRecommendedAction,
                QuickView = command.QuickView
            },
            cancellationToken);

        if (searchResult.TotalCount == 0)
        {
            return ValidationFailure(command.Mode, "No hay documentos internos en el conjunto filtrado para refrescar.");
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
                    SourceType = RepBaseDocumentSourceType.Internal.ToString(),
                    SourceId = x.FiscalDocumentId
                })
                .ToList()
        };
    }

    private static bool IsRefreshableStatus(InternalRepBaseDocumentPaymentComplementReadModel complement)
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
