using Kaleido;
using Kaleido.Observability;
using Kaleido.Process.Observability;
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

    void Canceled();

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

    private static readonly Counter<long> QueryExecutionCancellationsCounter =
        Meter.CreateCounter<long>(
            QueryableTelemetry.ExecutionCancellationsCounterName);

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
                QueryableTelemetry.ExecuteActivityName,
                ActivityKind.Internal);

        var correlation = correlationAccessor.Current;

        activity?.SetTag(KaleidoTelemetryTags.RequestId, correlation.RequestId);
        activity?.SetTag(KaleidoTelemetryTags.ProcessorInstanceId, correlation.ProcessorInstanceId?.ToString());
        activity?.SetTag(KaleidoTelemetryTags.SourceProcessor, correlation.SourceProcessorName);

        if (correlation.ProcessId.HasValue)
            activity?.SetTag(ProcessTelemetry.TagProcessId, correlation.ProcessId.Value.ToString());

        activity?.SetTag(QueryableTelemetry.TagQueryContext, details.QueryContextName);
        activity?.SetTag(QueryableTelemetry.TagQueryView, details.QueryViewName);
        activity?.SetTag(QueryableTelemetry.TagQueryDirect, details.IsDirectQuery);
        activity?.SetTag(QueryableTelemetry.TagQueryExecutionMode, details.ExecutionMode.ToString());

        QueryExecutionsCounter.Add(1, CreateExecutionTags(details));

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

    private static TagList CreateExecutionTags(QueryObservationDetails details)
    {
        TagList tags =
        [
            new("query.context", details.QueryContextName),
            new("query.direct", details.IsDirectQuery),
            new("query.execution_mode", details.ExecutionMode.ToString())
        ];

        if (!string.IsNullOrWhiteSpace(details.QueryViewName))
            tags.Add("query.view", details.QueryViewName);

        return tags;
    }

    private sealed class QueryExecutionObservation(
        Activity? activity,
        ILogger logger,
        QueryObservationDetails details)
        : IQueryExecutionObservation
    {
        public IDisposable BeginSource() =>
            BeginChild(QueryableTelemetry.SourceActivityName);

        public IDisposable BeginView() =>
            BeginChild(QueryableTelemetry.ViewActivityName);

        public IDisposable BeginMaterialization() =>
            BeginChild(QueryableTelemetry.MaterializeActivityName);

        public IDisposable BeginDelegate() =>
            BeginChild(QueryableTelemetry.DelegateActivityName);

        public void ValidationFailed(QueryableValidationException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag(QueryableTelemetry.TagValidationCode, exception.Code);

            QueryValidationFailuresCounter.Add(
                1,
                CreateValidationTags(details, exception.Code));

            logger.LogWarning(
                exception,
                "Queryable validation failed for context {QueryContextName} view {QueryViewName} with code {ValidationCode}.",
                details.QueryContextName,
                details.QueryViewName,
                exception.Code);
        }

        public void Materialized(int totalCount, int returnedCount, int? pageSize, int? pageOffset)
        {
            activity?.SetTag(QueryableTelemetry.TagTotalCount, totalCount);
            activity?.SetTag(QueryableTelemetry.TagReturnedCount, returnedCount);
            activity?.SetTag(QueryableTelemetry.TagPageSize, pageSize);
            activity?.SetTag(QueryableTelemetry.TagPageOffset, pageOffset);

            var tags = CreateExecutionTags(details);

            QueryTotalCountHistogram.Record(totalCount, tags);
            QueryReturnedCountHistogram.Record(returnedCount, tags);

            if (pageSize is not null)
                QueryPageSizeHistogram.Record(pageSize.Value, tags);

            if (pageOffset is not null)
                QueryPageOffsetHistogram.Record(pageOffset.Value, tags);

            logger.LogDebug(
                "Queryable materialization completed for context {QueryContextName} view {QueryViewName} total {TotalCount} returned {ReturnedCount} pageSize {PageSize} pageOffset {PageOffset}.",
                details.QueryContextName,
                details.QueryViewName,
                totalCount,
                returnedCount,
                pageSize,
                pageOffset);
        }

        public void Canceled()
        {
            activity?.AddEvent(new ActivityEvent(QueryableTelemetry.CanceledEventName));

            QueryExecutionCancellationsCounter.Add(1, CreateExecutionTags(details));

            logger.LogWarning(
                "Queryable execution was canceled for context {QueryContextName} view {QueryViewName}.",
                details.QueryContextName,
                details.QueryViewName);
        }

        public void ExecutionFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddEvent(new ActivityEvent(QueryableTelemetry.ExceptionEventName));

            QueryExecutionFailuresCounter.Add(1, CreateExecutionTags(details));

            logger.LogError(
                exception,
                "Queryable execution failed for context {QueryContextName} view {QueryViewName}.",
                details.QueryContextName,
                details.QueryViewName);
        }

        public void Dispose() => activity?.Dispose();

        private static TagList CreateValidationTags(QueryObservationDetails details, string validationCode)
        {
            var tags = CreateExecutionTags(details);
            tags.Add("validation.code", validationCode);
            return tags;
        }

        private static IDisposable BeginChild(string name)
        {
            var activity =
                ActivitySource.StartActivity(name, ActivityKind.Internal);

            return (IDisposable?)activity ?? NullDisposable.Instance;
        }
    }
}
