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
    public async Task<QueryResult<RequestingProviderSearchView>> SearchAsync(
        Guid processId,
        QueryBody? query,
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
            throw new InvalidOperationException("A captured member plan is required before requesting provider search can run.");
        }

        return await queryableClientFactory
            .GetClient("Provider")
            .QueryViewAsync<RequestingProviderSearchParameters, RequestingProviderSearchView>(
                "requesting-providers",
                "requesting-provider-search",
                new QueryApiRequest<RequestingProviderSearchParameters>
                {
                    Parameters = new RequestingProviderSearchParameters
                    {
                        PlanId = planId
                    },
                    Query = query
                },
                cancellationToken);
    }
}
