using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

internal sealed record QueryObservationDetails(
    string QueryContextName,
    string? QueryViewName,
    bool IsDirectQuery);

internal sealed class QueryableObservability
    : IQueryableObservability
{
    private const string ActivitySourceName =
        "Kaleido.Queryable";

    private static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);

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

        _logger.LogDebug(
            "Queryable execution started for context {QueryContextName} view {QueryViewName} direct {IsDirectQuery}.",
            details.QueryContextName,
            details.QueryViewName,
            details.IsDirectQuery);

        return new QueryExecutionObservation(
            activity,
            _logger,
            details);
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
            "kaleido.participant.id",
            correlation.ParticipantId);

        activity?.SetTag(
            "kaleido.participant.instance_id",
            correlation.ParticipantInstanceId?.ToString());

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
