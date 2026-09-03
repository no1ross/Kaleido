using Kaleido.Observability;
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

public enum QueryExecutionMode
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

internal sealed class QueryableObservability
    : IQueryableObservability
{
    private static readonly ActivitySource ActivitySource =
        new(QueryableTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(QueryableTelemetry.MeterName);

    private static readonly Counter<long> QueryExecutionsCounter =
        Meter.CreateCounter<long>(
            "kaleido.queryable.executions");

    private static readonly Counter<long> QueryValidationFailuresCounter =
        Meter.CreateCounter<long>(
            "kaleido.queryable.validation_failures");

    private static readonly Counter<long> QueryExecutionFailuresCounter =
        Meter.CreateCounter<long>(
            "kaleido.queryable.execution_failures");

    private static readonly Histogram<long> QueryTotalCountHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.queryable.total_count");

    private static readonly Histogram<long> QueryReturnedCountHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.queryable.returned_count");

    private static readonly Histogram<long> QueryPageSizeHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.queryable.page_size");

    private static readonly Histogram<long> QueryPageOffsetHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.queryable.page_offset");

    private readonly IKaleidoCorrelationContextAccessor _correlationAccessor;
    private readonly ILogger<QueryableObservability> _logger;

    public QueryableObservability(
        IKaleidoCorrelationContextAccessor correlationAccessor,
        ILogger<QueryableObservability> logger)
    {
        ArgumentNullException.ThrowIfNull(correlationAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    public IQueryExecutionObservation BeginExecution(
        QueryObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.queryable.execute",
                ActivityKind.Internal);

        var correlation =
            _correlationAccessor.Current;

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

        _logger.LogDebug(
            "Queryable execution started for context {QueryContextName} view {QueryViewName} direct {IsDirectQuery} mode {ExecutionMode}.",
            details.QueryContextName,
            details.QueryViewName,
            details.IsDirectQuery,
            details.ExecutionMode);

        return new QueryExecutionObservation(
            activity,
            _logger,
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
            "kaleido.processor.id",
            correlation.ProcessorId);

        activity?.SetTag(
            "kaleido.processor.instance_id",
            correlation.ProcessorInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.orchestrator.id",
            correlation.OrchestratorId);

        activity?.SetTag(
            "kaleido.orchestrator.instance_id",
            correlation.OrchestratorInstanceId?.ToString());
    }

    private sealed class QueryExecutionObservation
        : IQueryExecutionObservation
    {
        private readonly Activity? _activity;
        private readonly QueryObservationDetails _details;
        private readonly ILogger _logger;

        public QueryExecutionObservation(
            Activity? activity,
            ILogger logger,
            QueryObservationDetails details)
        {
            _activity = activity;
            _logger = logger;
            _details = details;
        }

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

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.SetTag(
                "kaleido.validation.code",
                exception.Code);

            QueryValidationFailuresCounter.Add(
                1,
                CreateValidationTags(
                    _details,
                    exception.Code));

            _logger.LogWarning(
                exception,
                "Queryable validation failed for context {QueryContextName} view {QueryViewName} with code {ValidationCode}.",
                _details.QueryContextName,
                _details.QueryViewName,
                exception.Code);
        }

        public void Materialized(
            int totalCount,
            int returnedCount,
            int? pageSize,
            int? pageOffset)
        {
            _activity?.SetTag(
                "kaleido.query.total_count",
                totalCount);

            _activity?.SetTag(
                "kaleido.query.returned_count",
                returnedCount);

            _activity?.SetTag(
                "kaleido.query.page_size",
                pageSize);

            _activity?.SetTag(
                "kaleido.query.page_offset",
                pageOffset);

            var tags =
                CreateExecutionTags(_details);

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

            _logger.LogDebug(
                "Queryable materialization completed for context {QueryContextName} view {QueryViewName} total {TotalCount} returned {ReturnedCount} pageSize {PageSize} pageOffset {PageOffset}.",
                _details.QueryContextName,
                _details.QueryViewName,
                totalCount,
                returnedCount,
                pageSize,
                pageOffset);
        }

        public void ExecutionFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.queryable.exception"));

            QueryExecutionFailuresCounter.Add(
                1,
                CreateExecutionTags(_details));

            _logger.LogError(
                exception,
                "Queryable execution failed for context {QueryContextName} view {QueryViewName}.",
                _details.QueryContextName,
                _details.QueryViewName);
        }

        public void Dispose()
        {
            _activity?.Dispose();
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

            return (IDisposable?)activity ?? NullScope.Instance;
        }
    }

    private sealed class NullScope
        : IDisposable
    {
        public static readonly NullScope Instance =
            new();

        public void Dispose()
        {
        }
    }
}
