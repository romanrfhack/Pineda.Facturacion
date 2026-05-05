using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class SearchStampedLegacyNotesReportService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private readonly IStampedLegacyNotesReportRepository _repository;

    public SearchStampedLegacyNotesReportService(IStampedLegacyNotesReportRepository repository)
    {
        _repository = repository;
    }

    public Task<SearchStampedLegacyNotesReportResult> ExecuteAsync(
        SearchStampedLegacyNotesReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? DefaultPage : filter.Page;
        var pageSize = filter.PageSize < 1 ? DefaultPageSize : Math.Min(filter.PageSize, MaxPageSize);
        var (fromUtc, toUtcExclusive) = MexicoLocalDateRangeConverter.ToUtcRange(filter.FromDate, filter.ToDate);

        return _repository.SearchAsync(new StampedLegacyNotesReportQuery
        {
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = page,
            PageSize = pageSize,
            ReceiverSearch = filter.ReceiverSearch,
            Uuid = filter.Uuid,
            Series = filter.Series,
            Folio = filter.Folio,
            LegacyOrderId = filter.LegacyOrderId,
            LegacyOrderNumber = filter.LegacyOrderNumber
        }, cancellationToken);
    }
}
