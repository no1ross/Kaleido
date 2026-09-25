using Kaleido.Http.Queryable;
using Kaleido.Http.Queryable;
using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Clients;

public sealed class PlanNetworkClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<IReadOnlySet<Guid>> GetNetworkIdsByPlanIdAsync(
        string planId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("ReferenceData")
            .QueryContextAsync<PlanQueryContext>(
                "plans",
                new QueryApiRequest
                {
                    Query = new QueryBody(
                        SearchText: planId,
                        Page: new QueryPage(
                            Size: 1,
                            Offset: 0))
                },
                cancellationToken);

        return result.Results
            .SelectMany(x => x.NetworkIds)
            .ToHashSet();
    }
}
