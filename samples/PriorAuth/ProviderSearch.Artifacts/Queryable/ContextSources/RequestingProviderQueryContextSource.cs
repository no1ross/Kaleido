using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Clients;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Parameters;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ContextSources;

internal sealed class RequestingProviderQueryContextSource(
    ProviderSearchDbContext dbContext,
    ReferenceDataClient referenceDataClient)
    : IQueryContextSource<RequestingProviderQueryContext>
{
    public IQueryable<RequestingProviderQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<RequestingProviderSearchParameters>()
            ?? throw new InvalidOperationException("RequestingProviderSearchParameters are required.");

        if (string.IsNullOrWhiteSpace(parameters.PlanId))
        {
            throw new InvalidOperationException("PlanId is required.");
        }

        var networkIds =
            referenceDataClient.GetNetworkIdsByPlanId(
                parameters.PlanId)
                .ToArray();

        return
            from location in dbContext.ProviderLocations.AsNoTracking()
            where location.Provider.ProviderType == ProviderType.RequestingProvider
            let isInNetwork = dbContext.ProviderLocationNetworks
                .Any(network =>
                    network.ProviderLocationId == location.ProviderLocationId
                    && networkIds.Contains(network.NetworkId))
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
            select new RequestingProviderQueryContext
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
                IsInNetwork = isInNetwork
            };
    }
}
