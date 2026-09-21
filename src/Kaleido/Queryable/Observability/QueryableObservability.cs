using Kaleido;
using Kaleido.Queryable.Exceptions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kaleido.Queryable.Observability;

internal interface IQueryableObservability
{
    IQueryExecutionObservation BeginExecution(
        QueryObservationDetails details);
}

internal interface IQueryExecutionObservation
    : IDisposable
{
    IDisposable BeginSource();

    IDisposable BeginView();

    IDisposable BeginMaterialization();

    IDisposable BeginDelegate();

    void ValidationFailed(
        QueryableValidationException exception);

    void Materialized(
        int totalCount,
        int returnedCount,
        int? pageSize,
        int? pageOffset);

    void ExecutionFailed(
        Exception exception);
}

internal enum QueryExecutionMode
{
    LocalView = 0,
    DirectContext = 1,
    DelegatedContext = 2
}

internal sealed record QueryObservationDetails(
    string QueryContextName,
    string? QueryViewName,
    bool IsDirectQuery,
    QueryExecutionMode ExecutionMode);

internal sealed class QueryableObservability(
    IKaleidoCorrelationContextAccessor correlationAccessor,
    ILogger<QueryableObservability> logger)
    : IQueryableObservability
{
    private static readonly ActivitySource ActivitySource =
        new(QueryableTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(QueryableTelemetry.MeterName);

    private static readonly Counter<long> QueryExecutionsCounter =
        Meter.CreateCounter<long>(
            QueryableTelemetry.ExecutionsCounterName);

    private static readonly Counter<long> QueryValidationFailuresCounter =
        Meter.CreateCounter<long>(
            QueryableTelemetry.ValidationFailuresCounterName);

    private static readonly Counter<long> QueryExecutionFailuresCounter =
        Meter.CreateCounter<long>(
            QueryableTelemetry.ExecutionFailuresCounterName);

    private static readonly Histogram<long> QueryTotalCountHistogram =
        Meter.CreateHistogram<long>(
            QueryableTelemetry.TotalCountHistogramName);

    private static readonly Histogram<long> QueryReturnedCountHistogram =
        Meter.CreateHistogram<long>(
            QueryableTelemetry.ReturnedCountHistogramName);

    private static readonly Histogram<long> QueryPageSizeHistogram =
        Meter.CreateHistogram<long>(
            QueryableTelemetry.PageSizeHistogramName);

    private static readonly Histogram<long> QueryPageOffsetHistogram =
        Meter.CreateHistogram<long>(
            QueryableTelemetry.PageOffsetHistogramName);



    public IQueryExecutionObservation BeginExecution(
        QueryObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.queryable.execute",
                ActivityKind.Internal);

        var correlation =
            correlationAccessor.Current;

        SetCorrelationTags(
            activity,
            correlation);

        activity?.SetTag(
            "kaleido.query.context",
            details.QueryContextName);

        activity?.SetTag(
            "kaleido.query.view",
            details.QueryViewName);

        activity?.SetTag(
            "kaleido.query.direct",
            details.IsDirectQuery);

        activity?.SetTag(
            "kaleido.query.execution_mode",
            details.ExecutionMode.ToString());

        QueryExecutionsCounter.Add(
            1,
            CreateExecutionTags(details));

        logger.LogDebug(
            "Queryable execution started for context {QueryContextName} view {QueryViewName} direct {IsDirectQuery} mode {ExecutionMode}.",
            details.QueryContextName,
            details.QueryViewName,
            details.IsDirectQuery,
            details.ExecutionMode);

        return new QueryExecutionObservation(
            activity,
            logger,
            details);
    }

    private static TagList CreateExecutionTags(
        QueryObservationDetails details)
    {
        TagList tags =
        [
            new("query.context", details.QueryContextName),
            new("query.direct", details.IsDirectQuery),
            new("query.execution_mode", details.ExecutionMode.ToString())
        ];

        if (!string.IsNullOrWhiteSpace(details.QueryViewName))
        {
            tags.Add(
                "query.view",
                details.QueryViewName);
        }

        return tags;
    }

    private static void SetCorrelationTags(
        Activity? activity,
        KaleidoCorrelationContext correlation)
    {
        activity?.SetTag(
            "kaleido.request.id",
            correlation.RequestId);

        activity?.SetTag(
            "kaleido.process.id",
            correlation.ProcessId?.ToString());

        activity?.SetTag(
            "kaleido.processor.instance_id",
            correlation.ProcessorInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.source.processor",
            correlation.SourceProcessorName);

    }

    private sealed class QueryExecutionObservation(
        Activity? activity,
        ILogger logger,
        QueryObservationDetails details)
        : IQueryExecutionObservation
    {

        public IDisposable BeginSource()
        {
            return BeginChild(
                "kaleido.queryable.source");
        }

        public IDisposable BeginView()
        {
            return BeginChild(
                "kaleido.queryable.view");
        }

        public IDisposable BeginMaterialization()
        {
            return BeginChild(
                "kaleido.queryable.materialize");
        }

        public IDisposable BeginDelegate()
        {
            return BeginChild(
                "kaleido.queryable.delegate");
        }

        public void ValidationFailed(
            QueryableValidationException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.SetTag(
                "kaleido.validation.code",
                exception.Code);

            QueryValidationFailuresCounter.Add(
                1,
                CreateValidationTags(
                    details,
                    exception.Code));

            logger.LogWarning(
                exception,
                "Queryable validation failed for context {QueryContextName} view {QueryViewName} with code {ValidationCode}.",
                details.QueryContextName,
                details.QueryViewName,
                exception.Code);
        }

        public void Materialized(
            int totalCount,
            int returnedCount,
            int? pageSize,
            int? pageOffset)
        {
            activity?.SetTag(
                "kaleido.query.total_count",
                totalCount);

            activity?.SetTag(
                "kaleido.query.returned_count",
                returnedCount);

            activity?.SetTag(
                "kaleido.query.page_size",
                pageSize);

            activity?.SetTag(
                "kaleido.query.page_offset",
                pageOffset);

            var tags =
                CreateExecutionTags(details);

            QueryTotalCountHistogram.Record(
                totalCount,
                tags);

            QueryReturnedCountHistogram.Record(
                returnedCount,
                tags);

            if (pageSize is not null)
            {
                QueryPageSizeHistogram.Record(
                    pageSize.Value,
                    tags);
            }

            if (pageOffset is not null)
            {
                QueryPageOffsetHistogram.Record(
                    pageOffset.Value,
                    tags);
            }

            logger.LogDebug(
                "Queryable materialization completed for context {QueryContextName} view {QueryViewName} total {TotalCount} returned {ReturnedCount} pageSize {PageSize} pageOffset {PageOffset}.",
                details.QueryContextName,
                details.QueryViewName,
                totalCount,
                returnedCount,
                pageSize,
                pageOffset);
        }

        public void ExecutionFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.queryable.exception"));

            QueryExecutionFailuresCounter.Add(
                1,
                CreateExecutionTags(details));

            logger.LogError(
                exception,
                "Queryable execution failed for context {QueryContextName} view {QueryViewName}.",
                details.QueryContextName,
                details.QueryViewName);
        }

        public void Dispose()
        {
            activity?.Dispose();
        }

        private static TagList CreateValidationTags(
            QueryObservationDetails details,
            string validationCode)
        {
            var tags =
                CreateExecutionTags(details);

            tags.Add(
                "validation.code",
                validationCode);

            return tags;
        }

        private static IDisposable BeginChild(
            string name)
        {
            var activity =
                ActivitySource.StartActivity(
                    name,
                    ActivityKind.Internal);

            return (IDisposable?)activity ?? NullDisposable.Instance;
        }
    }
}
