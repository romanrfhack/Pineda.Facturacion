namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableTimelineEntry
{
    public DateTime AtUtc { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public long SourceId { get; init; }

    public string? Status { get; init; }
}
