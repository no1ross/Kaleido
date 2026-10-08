using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Zip Codes",
    Description = "Zip codes with their city and state (reference data).",
    Source = "Prior Authorization Reference Data")]
[Pageable(
    DefaultSize = 25,
    MaxSize = 250)]
internal sealed class ZipCodeQueryContextSource(
    ReferenceDataDbContext dbContext)
    : IQuerySource<ZipCodeQueryContext>
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
