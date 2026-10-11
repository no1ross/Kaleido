using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Provider.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

// Delegated source, like RequestingProviderSearchSource: the consumer supplies the process id and
// its search; Radiology adds the member's plan and lets the Provider service search servicing
// facilities, with network status.
[QuerySource(
    Version = "1.0.0",
    DisplayName = "Servicing Provider Search",
    Description = "Searchable servicing providers (facilities where the service is performed) for the active radiology request, with network status.",
    Source = "Prior Authorization Radiology")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ServicingProviderSearchSource(
    RequestingProviderSearchClient providerSearchClient)
    : IDelegatedQuerySource<RequestingProviderSearchQueryContext, RequestingProviderSearchView, RequestingProviderSearchQueryParameters>
{
    public async Task<QueryResult<RequestingProviderSearchView>> ExecuteAsync(
        IQueryRequest<RequestingProviderSearchQueryParameters> request,
        CancellationToken cancellationToken = default)
    {
        var parameters =
            request.ViewParameters
            ?? throw new InvalidOperationException(
                $"{nameof(RequestingProviderSearchQueryParameters)} are required.");

        return await providerSearchClient.SearchAsync(
            parameters.ProcessId,
            request.Query,
            ProviderType.ServicingFacility,
            cancellationToken);
    }
}
