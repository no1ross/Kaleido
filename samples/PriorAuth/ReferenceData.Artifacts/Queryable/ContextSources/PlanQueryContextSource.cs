using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.ContextSources;

internal sealed class PlanQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQueryContextSource<PlanQueryContext>
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
