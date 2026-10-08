using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.ContextSources;

// Service-to-service only: queried by Provider (PlanNetworkClient).
[QuerySource(
    Version = "1.0.0",
    DisplayName = "Plans",
    Description = "Health plans (reference data).",
    Source = "Prior Authorization Reference Data")]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
[KaleidoAuthorization(Policy = DevAuthPolicies.InternalCaller)]
internal sealed class PlanQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQuerySource<PlanQueryContext>
{
    public IQueryable<PlanQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.Plans
            .AsNoTracking()
            .Select(plan =>
                new PlanQueryContext
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    LineOfBusiness = plan.LineOfBusiness,
                    StateCode = plan.StateCode,
                    EffectiveDate = plan.EffectiveDate,
                    TerminationDate = plan.TerminationDate,
                    IsActive = plan.IsActive,
                    NetworkIds = plan.PlanNetworks
                        .Select(planNetwork => planNetwork.NetworkId)
                        .ToArray()
                });
    }
}
