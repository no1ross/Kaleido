namespace Kaleido.Queryable.Observability;

/// <summary>
/// Activity source, meter, span, event, tag and instrument names of the Queryable runtime, part of
/// its stable telemetry contract. Register them with <c>AddKaleidoInstrumentation()</c> (or
/// <c>AddKaleidoQueryableInstrumentation()</c>) from <c>Kaleido.Observability.OpenTelemetry</c>.
/// </summary>
/// <remarks>
/// One execution is observed per query, whether it targets a local view, a local source directly,
/// or a delegated source. Metric instruments carry the <see cref="TagQuerySource"/>,
/// <see cref="TagQueryDirect"/> and <see cref="TagQueryExecutionMode"/> tags, plus
/// <see cref="TagQueryView"/> for a view query.
/// </remarks>
public static class QueryableTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    /// <summary>The <c>ActivitySource</c> of all Queryable spans.</summary>
    public const string ActivitySourceName =
        "Kaleido.Queryable";

    /// <summary>The <c>Meter</c> of all Queryable instruments.</summary>
    public const string MeterName =
        "Kaleido.Queryable";

    // ── Activity names ────────────────────────────────────────────────────────

    /// <summary>
    /// The span of one query execution, from validation to the materialized result. Tagged with
    /// the correlation context, the query source, view, direct flag and execution mode; marked as
    /// an error only when execution fails, not for validation failures or cancellation.
    /// </summary>
    public const string ExecuteActivityName =
        "kaleido.queryable.execute";

    /// <summary>
    /// Child span around a local source producing its queryable and Kaleido applying search,
    /// filter and sort to it.
    /// </summary>
    public const string SourceActivityName =
        "kaleido.queryable.source";

    /// <summary>Child span around a local query view projecting the source's queryable.</summary>
    public const string ViewActivityName =
        "kaleido.queryable.view";

    /// <summary>
    /// Child span around paging and materializing a local query's results, including the count
    /// query when one is needed.
    /// </summary>
    public const string MaterializeActivityName =
        "kaleido.queryable.materialize";

    /// <summary>Child span around a delegated source executing the query downstream.</summary>
    public const string DelegateActivityName =
        "kaleido.queryable.delegate";

    // ── Activity event names ──────────────────────────────────────────────────

    /// <summary>
    /// Event added to the <see cref="ExecuteActivityName"/> span when the execution is canceled.
    /// </summary>
    public const string CanceledEventName =
        "kaleido.queryable.canceled";

    // ── Tag key names (activity tags) ─────────────────────────────────────────

    /// <summary>The query source's public name (its type name). On the execute span and every Queryable metric.</summary>
    public const string TagQuerySource =
        "kaleido.query.source";

    /// <summary>
    /// The query view's public name, present only for a view query. On the execute span and every
    /// Queryable metric.
    /// </summary>
    public const string TagQueryView =
        "kaleido.query.view";

    /// <summary>
    /// Whether the query targets a source rather than a view (<see langword="true"/> for direct and
    /// delegated source queries). On the execute span and every Queryable metric.
    /// </summary>
    public const string TagQueryDirect =
        "kaleido.query.direct";

    /// <summary>
    /// How the query was executed: <c>LocalView</c>, <c>DirectSource</c> or <c>DelegatedSource</c>.
    /// On the execute span and every Queryable metric.
    /// </summary>
    public const string TagQueryExecutionMode =
        "kaleido.query.execution_mode";

    /// <summary>
    /// The <c>qry_*</c> code of a validation failure that refused the query. On the execute span
    /// and the <see cref="ValidationFailuresCounterName"/> counter.
    /// </summary>
    public const string TagValidationCode =
        "kaleido.validation.code";

    /// <summary>The result's total count of matching results, on the execute span of a successful query.</summary>
    public const string TagTotalCount =
        "kaleido.query.total_count";

    /// <summary>The number of records returned, on the execute span of a successful query.</summary>
    public const string TagReturnedCount =
        "kaleido.query.returned_count";

    /// <summary>The result's page size, on the execute span of a successful query.</summary>
    public const string TagPageSize =
        "kaleido.query.page_size";

    /// <summary>The result's page offset, on the execute span of a successful query.</summary>
    public const string TagPageOffset =
        "kaleido.query.page_offset";

    // ── Metric names ──────────────────────────────────────────────────────────

    /// <summary>Counter of query executions started, valid or not.</summary>
    public const string ExecutionsCounterName =
        "kaleido.queryable.executions";

    /// <summary>
    /// Counter of queries refused by validation (a caller error, not a failure of the service).
    /// Also tagged with <see cref="TagValidationCode"/>.
    /// </summary>
    public const string ValidationFailuresCounterName =
        "kaleido.queryable.validation_failures";

    /// <summary>
    /// Counter of query executions that failed with an exception other than a validation failure
    /// or a cancellation.
    /// </summary>
    public const string ExecutionFailuresCounterName =
        "kaleido.queryable.execution_failures";

    /// <summary>Counter of query executions canceled (<c>OperationCanceledException</c>); never counted as failures.</summary>
    public const string ExecutionCancellationsCounterName =
        "kaleido.queryable.execution_cancellations";

    /// <summary>Histogram of the total count of matching results of each successful query.</summary>
    public const string TotalCountHistogramName =
        "kaleido.queryable.total_count";

    /// <summary>Histogram of the number of records returned by each successful query.</summary>
    public const string ReturnedCountHistogramName =
        "kaleido.queryable.returned_count";

    /// <summary>Histogram of the page size of each successful query.</summary>
    public const string PageSizeHistogramName =
        "kaleido.queryable.page_size";

    /// <summary>Histogram of the page offset of each successful query.</summary>
    public const string PageOffsetHistogramName =
        "kaleido.queryable.page_offset";

    /// <summary>
    /// Histogram of the duration of each query execution in seconds (unit <c>s</c>), whatever its
    /// outcome.
    /// </summary>
    public const string ExecutionDurationHistogramName =
        "kaleido.queryable.execution.duration";
}
