using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class RequestingProviderSearchClient(
    QueryableHttpClient queryableHttpClient,
    IConfiguration configuration,
    Data.IntakeDbContext dbContext)
{
    private readonly string requestingProviderSearchQueryPath =
        configuration["Services:ProviderSearch:RequestingProviderSearchQueryPath"]
        ?? "/provider/queryable/requesting-providers/requesting-provider-search/query";

    public async Task<QueryableResult<RequestingProviderSearchRecord>> SearchAsync(
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

        return await queryableHttpClient.QueryAsync<ProviderRequestingSearchViewParameters, RequestingProviderSearchRecord, QueryableResult<RequestingProviderSearchRecord>>(
            "ProviderSearch",
            requestingProviderSearchQueryPath,
            new QueryApiRequest<ProviderRequestingSearchViewParameters>
            {
                Parameters = new ProviderRequestingSearchViewParameters
                {
                    PlanId = planId
                },
                Query = query
            },
            result => new QueryableResult<RequestingProviderSearchRecord>
            {
                Records = result.Records.ToArray()
            },
            cancellationToken);
    }

    public sealed class ProviderRequestingSearchViewParameters
    {
        public string PlanId { get; init; } = string.Empty;
    }
}
