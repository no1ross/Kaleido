using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.ContextSources;

internal sealed class ZipCodeQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQueryContextSource<ZipCodeQueryContext>
{
    public IQueryable<ZipCodeQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ZipCodes
            .AsNoTracking()
            .Select(zipCode =>
                new ZipCodeQueryContext
                {
                    PostalCode = zipCode.PostalCode,
                    StateCode = zipCode.StateCode,
                    City = zipCode.City,
                    IsActive = zipCode.IsActive
                });
    }
}
