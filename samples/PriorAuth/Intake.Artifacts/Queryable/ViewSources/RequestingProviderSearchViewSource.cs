using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "requesting-provider-search",
    DisplayName = "Intake - Requesting Provider Search",
    Version = "1.0.0",
    Description = "Searchable requesting provider results scoped to the active intake process.",
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
