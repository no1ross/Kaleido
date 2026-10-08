using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "States",
    Description = "States (reference data).",
    Source = "Prior Authorization Reference Data")]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
internal sealed class StateQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQuerySource<StateQueryContext>
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
