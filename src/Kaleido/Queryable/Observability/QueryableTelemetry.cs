namespace Kaleido.Queryable.Observability;

public static class QueryableTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    public const string ActivitySourceName =
        "Kaleido.Queryable";

    public const string MeterName =
        "Kaleido.Queryable";

    // ── Activity names ────────────────────────────────────────────────────────

    public const string ExecuteActivityName =
        "kaleido.queryable.execute";

    public const string SourceActivityName =
        "kaleido.queryable.source";

    public const string ViewActivityName =
        "kaleido.queryable.view";

    public const string MaterializeActivityName =
        "kaleido.queryable.materialize";

    public const string DelegateActivityName =
        "kaleido.queryable.delegate";

    // ── Activity event names ──────────────────────────────────────────────────

    public const string CanceledEventName =
        "kaleido.queryable.canceled";

    public const string ExceptionEventName =
        "kaleido.queryable.exception";

    // ── Tag key names (activity tags) ─────────────────────────────────────────

    public const string TagQueryContext =
        "kaleido.query.context";

    public const string TagQueryView =
        "kaleido.query.view";

    public const string TagQueryDirect =
        "kaleido.query.direct";

    public const string TagQueryExecutionMode =
        "kaleido.query.execution_mode";

    public const string TagValidationCode =
        "kaleido.validation.code";

    public const string TagTotalCount =
        "kaleido.query.total_count";

    public const string TagReturnedCount =
        "kaleido.query.returned_count";

    public const string TagPageSize =
        "kaleido.query.page_size";

    public const string TagPageOffset =
        "kaleido.query.page_offset";

    // ── Metric names ──────────────────────────────────────────────────────────

    public const string ExecutionsCounterName =
        "kaleido.queryable.executions";

    public const string ValidationFailuresCounterName =
        "kaleido.queryable.validation_failures";

    public const string ExecutionFailuresCounterName =
        "kaleido.queryable.execution_failures";

    public const string ExecutionCancellationsCounterName =
        "kaleido.queryable.execution_cancellations";

    public const string TotalCountHistogramName =
        "kaleido.queryable.total_count";

    public const string ReturnedCountHistogramName =
        "kaleido.queryable.returned_count";

    public const string PageSizeHistogramName =
        "kaleido.queryable.page_size";

    public const string PageOffsetHistogramName =
        "kaleido.queryable.page_offset";

    public const string ExecutionDurationHistogramName =
        "kaleido.queryable.execution.duration";
}
