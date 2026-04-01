namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class BulkRefreshRepBaseDocumentsService
{
    public const int MaxDocumentsPerRequest = 50;

    private readonly SearchRepBaseDocumentsService _searchService;
    private readonly BulkRefreshInternalRepBaseDocumentsService _internalService;
    private readonly BulkRefreshExternalRepBaseDocumentsService _externalService;

    public BulkRefreshRepBaseDocumentsService(
        SearchRepBaseDocumentsService searchService,
        BulkRefreshInternalRepBaseDocumentsService internalService,
        BulkRefreshExternalRepBaseDocumentsService externalService)
    {
        _searchService = searchService;
        _internalService = internalService;
        _externalService = externalService;
    }

    public async Task<RepBaseDocumentBulkRefreshResult> ExecuteAsync(
        BulkRefreshRepBaseDocumentsCommand command,
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

        var invalidItems = targetsResult.Items
            .Where(x => !x.Attempted)
            .ToList();

        var internalIds = targetsResult.Items
            .Where(x => x.Attempted && string.Equals(x.SourceType, RepBaseDocumentSourceType.Internal.ToString(), StringComparison.Ordinal))
            .Select(x => x.SourceId)
            .Distinct()
            .ToList();

        var externalIds = targetsResult.Items
            .Where(x => x.Attempted && string.Equals(x.SourceType, RepBaseDocumentSourceType.External.ToString(), StringComparison.Ordinal))
            .Select(x => x.SourceId)
            .Distinct()
            .ToList();

        if (internalIds.Count == 0 && externalIds.Count == 0)
        {
            return ValidationFailure(command.Mode, "Debes indicar al menos un documento válido para refrescar.");
        }

        var refreshedByKey = new Dictionary<string, RepBaseDocumentBulkRefreshItemResult>(StringComparer.Ordinal);

        if (internalIds.Count > 0)
        {
            var internalResult = await _internalService.ExecuteAsync(
                new BulkRefreshInternalRepBaseDocumentsCommand
                {
                    Mode = RepBaseDocumentBulkRefreshMode.Selected,
                    Documents = internalIds
                        .Select(id => new RepBaseDocumentBulkRefreshDocumentReference
                        {
                            SourceType = RepBaseDocumentSourceType.Internal.ToString(),
                            SourceId = id
                        })
                        .ToList()
                },
                cancellationToken);

            if (!internalResult.IsSuccess)
            {
                return ValidationFailure(command.Mode, internalResult.ErrorMessage ?? "No fue posible refrescar el conjunto interno.");
            }

            foreach (var item in internalResult.Items)
            {
                refreshedByKey[BuildKey(item.SourceType, item.SourceId)] = item;
            }
        }

        if (externalIds.Count > 0)
        {
            var externalResult = await _externalService.ExecuteAsync(
                new BulkRefreshExternalRepBaseDocumentsCommand
                {
                    Mode = RepBaseDocumentBulkRefreshMode.Selected,
                    Documents = externalIds
                        .Select(id => new RepBaseDocumentBulkRefreshDocumentReference
                        {
                            SourceType = RepBaseDocumentSourceType.External.ToString(),
                            SourceId = id
                        })
                        .ToList()
                },
                cancellationToken);

            if (!externalResult.IsSuccess)
            {
                return ValidationFailure(command.Mode, externalResult.ErrorMessage ?? "No fue posible refrescar el conjunto externo.");
            }

            foreach (var item in externalResult.Items)
            {
                refreshedByKey[BuildKey(item.SourceType, item.SourceId)] = item;
            }
        }

        var invalidByKey = invalidItems.ToDictionary(x => BuildKey(x.SourceType, x.SourceId), StringComparer.Ordinal);
        var orderedItems = new List<RepBaseDocumentBulkRefreshItemResult>();

        foreach (var target in targetsResult.Items)
        {
            var key = BuildKey(target.SourceType, target.SourceId);

            if (invalidByKey.TryGetValue(key, out var invalidItem))
            {
                orderedItems.Add(invalidItem);
                continue;
            }

            if (refreshedByKey.TryGetValue(key, out var refreshedItem))
            {
                orderedItems.Add(refreshedItem);
            }
        }

        return BuildSuccess(command.Mode, targetsResult.TotalRequested, orderedItems);
    }

    protected virtual async Task<RepBaseDocumentBulkRefreshResult> ResolveTargetsAsync(
        BulkRefreshRepBaseDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.Equals(command.Mode, RepBaseDocumentBulkRefreshMode.Selected, StringComparison.Ordinal))
        {
            var distinctDocuments = command.Documents
                .Where(x => x.SourceId > 0)
                .DistinctBy(x => BuildKey(x.SourceType, x.SourceId))
                .ToList();

            if (distinctDocuments.Count == 0)
            {
                return ValidationFailure(command.Mode, "Debes seleccionar al menos un documento para refrescar.");
            }

            if (distinctDocuments.Count > MaxDocumentsPerRequest)
            {
                return ValidationFailure(command.Mode, $"El límite máximo por operación es {MaxDocumentsPerRequest} documentos.");
            }

            return new RepBaseDocumentBulkRefreshResult
            {
                IsSuccess = true,
                Mode = command.Mode,
                MaxDocuments = MaxDocumentsPerRequest,
                TotalRequested = distinctDocuments.Count,
                Items = distinctDocuments
                    .Select(MapSelectedDocument)
                    .ToList()
            };
        }

        var searchResult = await _searchService.ExecuteAsync(
            new SearchRepBaseDocumentsFilter
            {
                Page = 1,
                PageSize = MaxDocumentsPerRequest,
                FromDate = command.FromDate,
                ToDate = command.ToDate,
                ReceiverRfc = command.ReceiverRfc,
                Query = command.Query,
                SourceType = command.SourceType,
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
            return ValidationFailure(command.Mode, "No hay documentos en el conjunto filtrado para refrescar.");
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
                    SourceType = x.SourceType,
                    SourceId = x.SourceId,
                    Attempted = true
                })
                .ToList()
        };
    }

    private static RepBaseDocumentBulkRefreshItemResult MapSelectedDocument(RepBaseDocumentBulkRefreshDocumentReference reference)
    {
        var normalizedSourceType = NormalizeSourceType(reference.SourceType);
        if (normalizedSourceType is null)
        {
            return new RepBaseDocumentBulkRefreshItemResult
            {
                SourceType = reference.SourceType?.Trim() ?? string.Empty,
                SourceId = reference.SourceId,
                Attempted = false,
                Outcome = RepBaseDocumentBulkRefreshItemOutcome.Failed,
                Message = "El origen del documento no es válido para refresh masivo."
            };
        }

        return new RepBaseDocumentBulkRefreshItemResult
        {
            SourceType = normalizedSourceType,
            SourceId = reference.SourceId,
            Attempted = true
        };
    }

    private static string? NormalizeSourceType(string? sourceType)
    {
        if (string.Equals(sourceType?.Trim(), RepBaseDocumentSourceType.Internal.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return RepBaseDocumentSourceType.Internal.ToString();
        }

        if (string.Equals(sourceType?.Trim(), RepBaseDocumentSourceType.External.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return RepBaseDocumentSourceType.External.ToString();
        }

        return null;
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

    private static string BuildKey(string? sourceType, long sourceId)
    {
        return $"{sourceType ?? string.Empty}:{sourceId}";
    }
}
