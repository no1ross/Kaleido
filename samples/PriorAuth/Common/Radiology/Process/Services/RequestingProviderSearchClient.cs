using Kaleido.Http.Queryable;
using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Parameters;
using Kaleido.Samples.PriorAuth.Provider.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class RequestingProviderSearchClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    RadiologyDbContext dbContext)
{
    /// <summary>
    /// The search result for one provider location, as the caller would see it for this request
    /// (scoped to the member's plan), or null when no such location is offered. Steps take the
    /// ids from a search result; this is how a handler checks them and reads the rest of the row
    /// (names, network status) instead of trusting values the caller sent.
    /// </summary>
    public async Task<RequestingProviderSearchView?> FindAsync(
        Guid processId,
        Guid providerLocationId,
        ProviderType providerType,
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(
            processId,
            new QueryBody(Filter: QueryFilterNode.CreateCondition(nameof(RequestingProviderSearchView.ProviderLocationId), FilterOperator.Equals, providerLocationId)),
            providerType,
            cancellationToken);

        return result.Results.SingleOrDefault();
    }

    public async Task<QueryResult<RequestingProviderSearchView>> SearchAsync(
        Guid processId,
        QueryBody? query,
        ProviderType providerType = ProviderType.RequestingProvider,
        CancellationToken cancellationToken = default)
    {
        var planId =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .Where(x => x.ProcessId == processId)
                .Select(x => x.Member != null ? x.Member.PlanId : null)
                .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(planId))
        {
            throw new InvalidOperationException("A captured member plan is required before a provider search can run.");
        }

        return await queryableClientFactory
            .GetClient("Provider")
            .QueryViewAsync<RequestingProviderSearchParameters, RequestingProviderSearchView>(
                "RequestingProviderQueryContextSource",
                "RequestingProviderSearchViewSource",
                new QueryApiRequest<RequestingProviderSearchParameters>
                {
                    Parameters = new RequestingProviderSearchParameters
                    {
                        PlanId = planId,
                        ProviderType = providerType
                    },
                    Query = query.ToApiBody()
                },
                cancellationToken);
    }
}
