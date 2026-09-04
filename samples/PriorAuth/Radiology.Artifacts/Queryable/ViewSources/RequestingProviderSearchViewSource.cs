using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.ViewSources;

[QueryView(
    Name = "requesting-provider-search",
    DisplayName = "Radiology - Requesting Provider Search",
    Version = "1.0.0",
    Description = "Searchable requesting provider results scoped to the active radiology process.",
    Visibility = QueryViewVisibility.Public,
    DefaultSortField = nameof(RequestingProviderSearchQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderSearchViewSource(
    RequestingProviderSearchClient requestingProviderSearchClient)
    : IDelegateQueryViewSource<RequestingProviderSearchQueryContext, RequestingProviderSearchRecord, RequestingProviderSearchQueryParameters>
{
    public async Task<QueryResult<RequestingProviderSearchRecord>> ExecuteAsync(
        IQueryRequest<RequestingProviderSearchQueryParameters> request,
        CancellationToken cancellationToken = default)
    {
        var parameters =
            request.ViewParameters
            ?? throw new InvalidOperationException(
                $"{nameof(RequestingProviderSearchQueryParameters)} are required.");

        return await requestingProviderSearchClient.SearchAsync(
            parameters.ProcessId,
            request.Query,
            cancellationToken);
    }
}
