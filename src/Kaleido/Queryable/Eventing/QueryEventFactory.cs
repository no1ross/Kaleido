using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Eventing;

internal interface IQueryEventFactory
{
    KaleidoEventEnvelope<QueryExecuted, QueryableEventContext> CreateQueryExecuted<TView>(
        KaleidoCorrelationContext correlation,
        QueryObservationDetails details,
        IQueryRequest request,
        QueryResult<TView> result)
        where TView : class;
}

internal sealed class QueryEventFactory(
    KaleidoServiceOptions serviceOptions)
    : IQueryEventFactory
{
    public KaleidoEventEnvelope<QueryExecuted, QueryableEventContext> CreateQueryExecuted<TView>(
        KaleidoCorrelationContext correlation,
        QueryObservationDetails details,
        IQueryRequest request,
        QueryResult<TView> result)
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var @event = new QueryExecuted
        {
            OccurredOn = DateTimeOffset.UtcNow,
            QueryContextName = details.QueryContextName,
            QueryViewName = details.QueryViewName,
            IsDirectQuery = details.IsDirectQuery,
            ExecutionMode = details.ExecutionMode.ToString(),
            Request = request,
            TotalCount = result.TotalCount,
            ReturnedCount = result.Results.Count,
            PageSize = result.PageSize,
            Offset = result.Offset,
            Records = result.Results.Cast<object?>().ToArray(),
            SearchText = (request.Query as QueryBody)?.SearchText,
            SortCount = request.Query?.Sort?.Count ?? 0,
            FilterProvided = request.Query?.Filter is not null,
            ViewParameters = request.ViewParameters
        };

        var context = new QueryableEventContext
        {
            RequestId = correlation.RequestId,
            ServiceName = serviceOptions.ServiceName,
            ProcessId = correlation.ProcessId,
            StepName = correlation.StepName,
            QueryContextName = details.QueryContextName,
            QueryViewName = details.QueryViewName,
            ProcessorInstanceId = serviceOptions.InstanceId.ToString(),
            SourceProcessorName = correlation.SourceProcessorName ?? serviceOptions.ServiceName
        };

        return new KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>
        {
            EventType = KaleidoEventTypes.QueryExecuted,
            Context = context,
            Event = @event
        };
    }
}
