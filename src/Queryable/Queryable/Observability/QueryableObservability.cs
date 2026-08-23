using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
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

    void Failed(
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

    public QueryableObservability(
        IKaleidoCorrelationContextAccessor correlationAccessor)
    {
        ArgumentNullException.ThrowIfNull(correlationAccessor);

        _correlationAccessor = correlationAccessor;
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

        return new QueryExecutionObservation(
            activity);
    }

    private static void SetCorrelationTags(
        Activity? activity,
        KaleidoCorrelationContext correlation)
    {
        activity?.SetTag(
            "kaleido.request.id",
            correlation.RequestId);

        activity?.SetTag(
            "kaleido.participant.process_instance_id",
            correlation.ParticipantProcessInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.orchestrator.process_instance_id",
            correlation.OrchestratorProcessInstanceId?.ToString());
    }

    private sealed class QueryExecutionObservation
        : IQueryExecutionObservation
    {
        private readonly Activity? _activity;

        public QueryExecutionObservation(
            Activity? activity)
        {
            _activity = activity;
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
        }

        public void Failed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.queryable.exception"));
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
