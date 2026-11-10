namespace Kaleido.Queryable.Eventing;

/// <summary>Marker for the events emitted by the Queryable runtime.</summary>
public interface IQueryEvent : IKaleidoEvent;

/// <summary>
/// A Queryable source or view query completed successfully. Event type
/// <see cref="KaleidoEventTypes.QueryExecuted"/>, published with a
/// <see cref="QueryableEventContext"/>.
/// </summary>
/// <remarks>
/// Emitted once per successful query, for local view, direct source and delegated source queries
/// alike; a query refused by validation, canceled or failed emits none. The event is written to
/// the event outbox only when an event publisher is registered. It carries the request and every
/// returned record, so publishers should treat it as containing the queried data.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record QueryExecuted : IQueryEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc/>
    public required DateTimeOffset OccurredOn { get; init; }

    /// <summary>The query source's public name (its type name).</summary>
    public required string QuerySourceName { get; init; }

    /// <summary>The query view's public name, or <see langword="null"/> for a direct or delegated source query.</summary>
    public string? QueryViewName { get; init; }

    /// <summary>
    /// <see langword="true"/> when the query targeted a source rather than a view (direct and
    /// delegated source queries).
    /// </summary>
    public required bool IsDirectQuery { get; init; }

    /// <summary>
    /// How the query was executed: <c>LocalView</c>, <c>DirectSource</c> or <c>DelegatedSource</c>.
    /// </summary>
    public string? ExecutionMode { get; init; }

    /// <summary>The executed request (an <see cref="IQueryRequest"/>), including its query body and parameters.</summary>
    public object? Request { get; init; }

    /// <summary>
    /// The result's <see cref="QueryResult{TView}.TotalCount"/>: the number of matching results,
    /// whatever page was returned.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>The number of records returned (the size of <see cref="Records"/>).</summary>
    public required int ReturnedCount { get; init; }

    /// <summary>The result's <see cref="QueryResult{TView}.PageSize"/>.</summary>
    public int? PageSize { get; init; }

    /// <summary>The result's <see cref="QueryResult{TView}.Offset"/>.</summary>
    public int? Offset { get; init; }

    /// <summary>The returned records, in result order.</summary>
    public IReadOnlyCollection<object?> Records { get; init; } = [];

    /// <summary>The request's search text, or <see langword="null"/> when it had none.</summary>
    public string? SearchText { get; init; }

    /// <summary>The number of sort keys in the request (zero when it had none).</summary>
    public required int SortCount { get; init; }

    /// <summary>Whether the request carried a filter.</summary>
    public required bool FilterProvided { get; init; }

    /// <summary>The request's parameters record, or <see langword="null"/> when none was supplied.</summary>
    public object? ViewParameters { get; init; }
}
