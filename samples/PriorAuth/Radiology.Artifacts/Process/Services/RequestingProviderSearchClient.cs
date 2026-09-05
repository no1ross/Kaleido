using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class RequestingProviderSearchClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration,
    RadiologyDbContext dbContext)
{
    private readonly string requestingProviderSearchView =
        configuration["Services:ProviderSearch:RequestingProviderSearchView"]
        ?? "RequestingProviderSearch";

    public async Task<QueryResult<RequestingProviderSearchRecord>> SearchAsync(
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
            .GetClient("ProviderSearch")
            .QueryViewAsync<ProviderRequestingSearchViewParameters, RequestingProviderSearchRecord>(
                "RequestingProviders",
                requestingProviderSearchView,
                new QueryApiRequest<ProviderRequestingSearchViewParameters>
                {
                    Parameters = new ProviderRequestingSearchViewParameters
                    {
                        PlanId = planId
                    },
                    Query = query
                },
                cancellationToken);
    }

    public sealed class ProviderRequestingSearchViewParameters
    {
        public string PlanId { get; init; } = string.Empty;
    }
}
