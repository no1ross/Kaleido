using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.Query;

internal interface IQueryEventFactory
{
    QueryExecuted CreateQueryExecuted<TView>(
        KaleidoCorrelationContext correlation,
        QueryObservationDetails details,
        IQueryRequest request,
        QueryResult<TView> result)
        where TView : class;
}

internal sealed class QueryEventFactory
    : IQueryEventFactory
{
    public QueryExecuted CreateQueryExecuted<TView>(
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

        return new QueryExecuted
        {
            ProcessId = correlation.ProcessId,
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
    }
}
