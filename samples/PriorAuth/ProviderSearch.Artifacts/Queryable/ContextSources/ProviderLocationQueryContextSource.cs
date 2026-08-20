using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ContextSources;

internal sealed class ProviderLocationQueryContextSource(
    ProviderSearchDbContext dbContext)
    : IQueryContextSource<ProviderLocationQueryContext>
{
    public IQueryable<ProviderLocationQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ProviderLocations
            .AsNoTracking()
            .Select(location =>
                new ProviderLocationQueryContext
                {
                    ProviderLocationId = location.ProviderLocationId,
                    ProviderId = location.ProviderId,
                    ProviderName = location.Provider.ProviderName,
                    LocationName = location.LocationName,
                    StateCode = location.StateCode,
                    PostalCode = location.PostalCode,
                    City = location.City,
                    IsActive = location.IsActive
                });
    }
}
