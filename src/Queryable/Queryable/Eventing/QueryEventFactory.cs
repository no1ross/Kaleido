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
        CompiledRecordQuery compiled,
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
        CompiledRecordQuery compiled,
        QueryResult<TView> result)
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(result);

        return new QueryExecuted
        {
            ProcessId = correlation.ProcessId,
            OccurredOn = DateTimeOffset.UtcNow,
            QueryContextName = details.QueryContextName,
            QueryViewName = details.QueryViewName,
            IsDirectQuery = details.IsDirectQuery,
            Request = request,
            TotalCount = result.TotalCount,
            ReturnedCount = result.Records.Count,
            PageSize = result.PageSize,
            Offset = result.Offset,
            Records = result.Records.Cast<object?>().ToArray(),
            SearchText = (request.Query as QueryBody)?.SearchText,
            SortCount = request.Query?.Sort?.Count ?? 0,
            FilterProvided = request.Query?.Filter is not null,
            ViewParameters = request.ViewParameters
        };
    }
}
