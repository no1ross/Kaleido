using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.ContextSources;

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
