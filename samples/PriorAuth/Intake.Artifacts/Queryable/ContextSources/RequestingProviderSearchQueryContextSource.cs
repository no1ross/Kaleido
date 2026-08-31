using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.ContextSources;

internal sealed class RequestingProviderSearchQueryContextSource(
    RequestingProviderSearchClient requestingProviderSearchClient)
    : IDelegatedQueryContextSource<RequestingProviderSearchQueryContext, RequestingProviderSearchRecord>
{
    public async Task<QueryResult<RequestingProviderSearchRecord>> ExecuteAsync(
        IQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ViewParameters is not RequestingProviderSearchQueryParameters parameters)
        {
            throw new InvalidOperationException("RequestingProviderSearchQueryParameters are required.");
        }

        var result =
            await requestingProviderSearchClient.SearchAsync(
                parameters.ProcessId,
                request.Query,
                cancellationToken);

        return new QueryResult<RequestingProviderSearchRecord>(
            result.TotalCount,
            result.Offset,
            result.PageSize,
            result.Records.ToArray());
    }
}
