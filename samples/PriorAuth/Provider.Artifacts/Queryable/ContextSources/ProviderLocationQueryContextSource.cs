using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Provider;
using Kaleido.Samples.PriorAuth.Provider.Data;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.ContextSources;

internal sealed class ProviderLocationQueryContextSource(
    ProviderSearchDbContext dbContext)
    : IQueryContextSource<ProviderLocationQueryContext>
{
    public IQueryable<ProviderLocationQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        var providerLocations =
            from location in dbContext.ProviderLocations.AsNoTracking()
            from primaryTin in dbContext.ProviderIdentifiers
                .Where(identifier =>
                    identifier.ProviderId == location.ProviderId
                    && identifier.IdentifierType == ProviderIdentifierType.TIN
                    && identifier.IsPrimary)
                .Select(identifier => identifier.IdentifierValue)
                .DefaultIfEmpty()
            from primaryNpi in dbContext.ProviderIdentifiers
                .Where(identifier =>
                    identifier.ProviderId == location.ProviderId
                    && identifier.IdentifierType == ProviderIdentifierType.NPI
                    && identifier.IsPrimary)
                .Select(identifier => identifier.IdentifierValue)
                .DefaultIfEmpty()
            from primarySpecialtyId in dbContext.ProviderLocationSpecialties
                .Where(specialty =>
                    specialty.ProviderLocationId == location.ProviderLocationId
                    && specialty.IsPrimary)
                .Select(specialty => (Guid?)specialty.MedicalSpecialtyId)
                .DefaultIfEmpty()
            select new ProviderLocationQueryContext
            {
                ProviderLocationId = location.ProviderLocationId,
                ProviderId = location.ProviderId,
                ProviderName = location.Provider.ProviderName,
                LocationName = location.LocationName,
                StateCode = location.StateCode,
                PostalCode = location.PostalCode,
                City = location.City,
                PhoneNumber = location.PhoneNumber,
                PrimaryTin = primaryTin,
                PrimaryNpi = primaryNpi,
                PrimaryMedicalSpecialtyId = primarySpecialtyId,
                PrimaryMedicalSpecialtyName = primarySpecialtyId == null ? null : "Radiology",
                PrimaryMedicalSpecialtyCode = primarySpecialtyId == null ? null : "RAD",
                ProviderType = location.Provider.ProviderType,
                IsActive = location.IsActive
            };

        return providerLocations;
    }
}
