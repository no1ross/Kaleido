using Kaleido.Http.Queryable;
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
            .QuerySourceAsync<PlanQueryContext>(
                "PlanQueryContextSource",
                new QueryApiRequest
                {
                    Query = new QueryApiBody
                    {
                        SearchText = planId,
                        Page = new QueryApiPage { Size = 1, Offset = 0 }
                    }
                },
                cancellationToken);

        return result.Results
            .SelectMany(x => x.NetworkIds)
            .ToHashSet();
    }
}
