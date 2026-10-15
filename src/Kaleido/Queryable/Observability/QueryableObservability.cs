using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kaleido.Processor.Observability;
using Microsoft.Extensions.Logging;

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
        KaleidoValidationException exception);

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
    DirectSource = 1,
    DelegatedSource = 2,
}

[ExcludeFromCodeCoverage]
internal sealed record QueryObservationDetails(
    string QuerySourceName,
    string? QueryViewName,
    bool IsDirectQuery,
    QueryExecutionMode ExecutionMode);

internal sealed partial class QueryableObservability(
    IKaleidoCorrelationContextAccessor correlationAccessor,
    KaleidoServiceOptions serviceOptions,
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

    private static readonly Histogram<double> QueryExecutionDurationHistogram =
        Meter.CreateHistogram<double>(
            QueryableTelemetry.ExecutionDurationHistogramName,
            unit: "s");

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
        activity?.SetTag(KaleidoTelemetryTags.ProcessorInstanceId, serviceOptions.InstanceId.ToString());
        activity?.SetTag(KaleidoTelemetryTags.CallingProcessor, correlation.CallingProcessorName);
        activity?.SetTag(KaleidoTelemetryTags.CallingStep, correlation.CallingStepName);

        if (correlation.ProcessId.HasValue)
        {
            activity?.SetTag(ProcessorTelemetry.TagProcessId, correlation.ProcessId.Value.ToString());
        }

        activity?.SetTag(QueryableTelemetry.TagQuerySource, details.QuerySourceName);
        activity?.SetTag(QueryableTelemetry.TagQueryView, details.QueryViewName);
        activity?.SetTag(QueryableTelemetry.TagQueryDirect, details.IsDirectQuery);
        activity?.SetTag(QueryableTelemetry.TagQueryExecutionMode, details.ExecutionMode.ToString());

        QueryExecutionsCounter.Add(1, CreateExecutionTags(details));

        var capability = Capability(details);

        LogExecutionStarted(logger, capability, details.IsDirectQuery, details.ExecutionMode);

        return new QueryExecutionObservation(
            activity,
            logger,
            details,
            capability);
    }

    // "Source" for a direct query, "Source/View" for a view: no "view (null)" in the logs.
    private static string Capability(QueryObservationDetails details) =>
        details.QueryViewName is { } view ? $"{details.QuerySourceName}/{view}" : details.QuerySourceName;

    private static TagList CreateExecutionTags(QueryObservationDetails details)
    {
        TagList tags =
        [
            new(QueryableTelemetry.TagQuerySource, details.QuerySourceName),
            new(QueryableTelemetry.TagQueryDirect, details.IsDirectQuery),
            new(QueryableTelemetry.TagQueryExecutionMode, details.ExecutionMode.ToString()),
        ];

        if (!string.IsNullOrWhiteSpace(details.QueryViewName))
        {
            tags.Add(QueryableTelemetry.TagQueryView, details.QueryViewName);
        }

        return tags;
    }

    private sealed partial class QueryExecutionObservation(
        Activity? activity,
        ILogger logger,
        QueryObservationDetails details,
        string capability)
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

        public void ValidationFailed(KaleidoValidationException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            // The caller's query was refused (400): tagged and counted, but not an error of this
            // service, and logged once at Warning by the transport.
            activity?.SetTag(QueryableTelemetry.TagValidationCode, exception.Code);

            QueryValidationFailuresCounter.Add(
                1,
                CreateValidationTags(details, exception.Code));

            LogValidationFailed(logger, exception, capability, exception.Code);
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
            {
                QueryPageSizeHistogram.Record(pageSize.Value, tags);
            }

            if (pageOffset is not null)
            {
                QueryPageOffsetHistogram.Record(pageOffset.Value, tags);
            }

            LogMaterialized(logger, capability, totalCount, returnedCount, pageSize, pageOffset);
        }

        public void Canceled()
        {
            activity?.AddEvent(new ActivityEvent(QueryableTelemetry.CanceledEventName));

            QueryExecutionCancellationsCounter.Add(1, CreateExecutionTags(details));

            LogCanceled(logger, capability);
        }

        public void ExecutionFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddExceptionEvent(exception);

            QueryExecutionFailuresCounter.Add(1, CreateExecutionTags(details));

            LogFailed(logger, exception, capability);
        }

        public void Dispose()
        {
            QueryExecutionDurationHistogram.Record(
                Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds,
                CreateExecutionTags(details));

            activity?.Dispose();
        }

        private readonly long _startTimestamp = Stopwatch.GetTimestamp();

        private static TagList CreateValidationTags(QueryObservationDetails details, string validationCode)
        {
            var tags = CreateExecutionTags(details);
            tags.Add(QueryableTelemetry.TagValidationCode, validationCode);
            return tags;
        }

        private static IDisposable BeginChild(string name)
        {
            var activity =
                ActivitySource.StartActivity(name, ActivityKind.Internal);

            return (IDisposable?)activity ?? NullDisposable.Instance;
        }

        [LoggerMessage(Level = LogLevel.Debug, Message = "Queryable validation failed for {Capability} with code {ValidationCode}.")]
        private static partial void LogValidationFailed(ILogger logger, Exception exception, string capability, string validationCode);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Queryable materialization completed for {Capability} total {TotalCount} returned {ReturnedCount} pageSize {PageSize} pageOffset {PageOffset}.")]
        private static partial void LogMaterialized(ILogger logger, string capability, int totalCount, int returnedCount, int? pageSize, int? pageOffset);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Queryable execution was canceled for {Capability}.")]
        private static partial void LogCanceled(ILogger logger, string capability);

        [LoggerMessage(Level = LogLevel.Error, Message = "Queryable execution failed for {Capability}.")]
        private static partial void LogFailed(ILogger logger, Exception exception, string capability);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Queryable execution started for {Capability} direct {IsDirectQuery} mode {ExecutionMode}.")]
    private static partial void LogExecutionStarted(ILogger logger, string capability, bool isDirectQuery, QueryExecutionMode executionMode);
}
