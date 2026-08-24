namespace Kaleido.Queryable.Eventing;

public sealed record QueryExecuted : IQueryEvent
{
    public Guid? ProcessId { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }

    public required string QueryContextName { get; init; }

    public string? QueryViewName { get; init; }

    public required bool IsDirectQuery { get; init; }

    public object? Request { get; init; }

    public required int TotalCount { get; init; }

    public required int ReturnedCount { get; init; }

    public int? PageSize { get; init; }

    public int? Offset { get; init; }

    public IReadOnlyCollection<object?> Records { get; init; } = [];

    public string? SearchText { get; init; }

    public required int SortCount { get; init; }

    public required bool FilterProvided { get; init; }

    public object? ViewParameters { get; init; }
}
