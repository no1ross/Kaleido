using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.ContextSources;

internal sealed class StateQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQueryContextSource<StateQueryContext>
{
    public IQueryable<StateQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.States
            .AsNoTracking()
            .Select(state =>
                new StateQueryContext
                {
                    StateCode = state.StateCode,
                    Name = state.Name,
                    IsActive = state.IsActive
                });
    }
}
