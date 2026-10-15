using Kaleido.Exceptions;
using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido.Samples.PriorAuth.Provider.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

// Delegated source: the consumer only supplies the process id and its search; this source adds
// what Radiology knows about the request (the member and their coverage) and lets the Provider
// service do the actual search, paging and sorting.
[QuerySource(
    Version = "1.0.0",
    DisplayName = "Requesting Provider Search",
    Description = "Searchable requesting provider results scoped to the active radiology process.",
    Source = "Prior Authorization Radiology")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderSearchSource(
    RequestingProviderSearchClient requestingProviderSearchClient)
    : IDelegatedQuerySource<RequestingProviderSearchQueryContext, RequestingProviderSearchView, RequestingProviderSearchQueryParameters>
{
    public async Task<QueryResult<RequestingProviderSearchView>> ExecuteAsync(
        IQueryRequest<RequestingProviderSearchQueryParameters> request,
        CancellationToken cancellationToken = default)
    {
        // Kaleido validates the [Required] processId before the query runs; this guard keeps the
        // nullable contract explicit and answers 400 if it is ever reached.
        var processId =
            request.ViewParameters?.ProcessId
            ?? throw new KaleidoValidationException(
                PriorAuthErrorCodes.QueryParametersRequired,
                "processId is required: the prior authorization this search is for.");

        return await requestingProviderSearchClient.SearchAsync(
            processId,
            request.Query,
            cancellationToken: cancellationToken);
    }
}
