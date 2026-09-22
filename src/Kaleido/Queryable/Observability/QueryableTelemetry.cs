namespace Kaleido.Queryable.Observability;

public static class QueryableTelemetry
{
    public const string ActivitySourceName =
        "Kaleido.Queryable";

    public const string MeterName =
        "Kaleido.Queryable";

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
}
