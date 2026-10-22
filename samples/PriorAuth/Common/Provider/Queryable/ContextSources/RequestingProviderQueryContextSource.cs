using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido.Samples.PriorAuth.Provider.Data;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Clients;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Parameters;
using Microsoft.EntityFrameworkCore;
using Kaleido.Exceptions;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Network Providers",
    Description = "Requesting providers or servicing facilities, with their locations and network status for a plan.",
    Source = "Prior Authorization Provider Search",
    DefaultSortField = nameof(RequestingProviderQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderQueryContextSource(
    ProviderSearchDbContext dbContext,
    PlanNetworkClient planNetworkClient)
    : IQuerySourceAsync<RequestingProviderQueryContext>
{
    public async Task<IQueryable<RequestingProviderQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        // Kaleido validates [Required] parameters before the query runs; these guards keep the
        // nullable contract explicit and answer 400 if they are ever reached.
        var parameters =
            executionContext.TryGetViewParameters<RequestingProviderSearchParameters>()
            ?? throw new KaleidoValidationException(PriorAuthErrorCodes.QueryParametersRequired, "The provider search parameters (planId) are required.");

        if (string.IsNullOrWhiteSpace(parameters.PlanId))
        {
            throw new KaleidoValidationException(PriorAuthErrorCodes.PlanIdRequired, "PlanId is required.");
        }

        var networkIds =
            (await planNetworkClient.GetNetworkIdsByPlanIdAsync(
                parameters.PlanId,
                cancellationToken))
            .ToArray();

        return
            from location in dbContext.ProviderLocations.AsNoTracking()
            where location.Provider.ProviderType == parameters.ProviderType
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
