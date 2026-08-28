using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Kaleido.Samples.PriorAuth.Seeder.ProviderSearch.ReferenceData;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch;

internal sealed class ProviderSearchSeeder(
    ServiceProjectContextFactory projectContextFactory,
    JsonAssetLoader jsonAssetLoader)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.ProviderSearch;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var serviceProvider =
            projectContextFactory.CreateSqliteDbContextProvider<ProviderSearchDbContext>(
                connectionString: "Data Source=providersearch.db");

        await using var scope =
            serviceProvider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ProviderSearchDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();
        ValidateSettings(assets.Settings);
        var zipCodesByState = assets.ZipCodes
            .Where(x => x.IsActive)
            .Where(x => assets.Settings.AllowedStates.Contains(x.StateCode, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.StateCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var planNetworksByState = assets.PlanNetworks
            .Join(
                LoadPlans(),
                planNetwork => planNetwork.PlanId,
                plan => plan.PlanId,
                (planNetwork, plan) => new { planNetwork, plan.StateCode })
            .Where(x => assets.Settings.AllowedStates.Contains(x.StateCode, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.StateCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.planNetwork).ToList(),
                StringComparer.OrdinalIgnoreCase);
        var primarySpecialty = assets.Specialties.First();

        var providers = new List<Provider>(assets.Settings.ProviderCount);
        var identifiers = new List<ProviderIdentifier>(assets.Settings.ProviderCount * 2);
        var locations = new List<ProviderLocation>();
        var locationNetworks = new List<ProviderLocationNetwork>();
        var locationSpecialties = new List<ProviderLocationSpecialty>();

        var requestingProviderCount =
            (int)Math.Round(assets.Settings.ProviderCount * (assets.Settings.RequestingProviderPercentage / 100d));

        for (var providerIndex = 1; providerIndex <= assets.Settings.ProviderCount; providerIndex++)
        {
            var providerType = providerIndex <= requestingProviderCount
                ? ProviderType.RequestingProvider
                : ProviderType.ServicingFacility;
            var provider = CreateProvider(providerIndex, providerType, assets);
            providers.Add(provider);

            identifiers.AddRange(CreateIdentifiers(providerIndex, provider, assets.Settings.BaseEffectiveDate));

            var providerLocations = CreateLocations(
                providerIndex,
                provider,
                providerType,
                assets,
                zipCodesByState);

            locations.AddRange(providerLocations);

            foreach (var location in providerLocations)
            {
                locationNetworks.AddRange(
                    CreateLocationNetworks(
                        providerIndex,
                        location,
                        providerType,
                        assets.Settings.BaseEffectiveDate,
                        planNetworksByState));

                locationSpecialties.Add(
                    new ProviderLocationSpecialty
                    {
                        ProviderLocationId = location.ProviderLocationId,
                        MedicalSpecialtyId = primarySpecialty.MedicalSpecialtyId,
                        IsPrimary = true,
                        EffectiveDate = assets.Settings.BaseEffectiveDate,
                        TerminationDate = null
                    });
            }
        }

        dbContext.Providers.AddRange(providers);
        dbContext.ProviderIdentifiers.AddRange(identifiers);
        dbContext.ProviderLocations.AddRange(locations);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ProviderLocationNetworks.AddRange(locationNetworks);
        dbContext.ProviderLocationSpecialties.AddRange(locationSpecialties);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private ProviderSearchSeedAssets LoadAssets()
    {
        const string basePath = "providersearch";

        return new ProviderSearchSeedAssets
        {
            PersonFirstNames = LoadRequiredStringList(basePath, "person-first-names.json"),
            PersonLastNames = LoadRequiredStringList(basePath, "person-last-names.json"),
            FacilityPrefixes = LoadRequiredStringList(basePath, "facility-prefixes.json"),
            FacilitySpecialties = LoadRequiredStringList(basePath, "facility-specialties.json"),
            FacilitySuffixes = LoadRequiredStringList(basePath, "facility-suffixes.json"),
            StreetNames = LoadRequiredStringList(basePath, "street-names.json"),
            StreetSuffixes = LoadRequiredStringList(basePath, "street-suffixes.json"),
            LocationQualifiers = LoadRequiredStringList(basePath, "location-qualifiers.json"),
            Settings = jsonAssetLoader.Load<ProviderSearchSeedSettings>(Path.Combine(basePath, "seed-settings.json")),
            PlanNetworks = jsonAssetLoader.Load<List<PlanNetworkSeedRecord>>(Path.Combine("referencedata", "plan-networks.json")),
            ZipCodes = jsonAssetLoader.Load<List<ZipCode>>(Path.Combine("referencedata", "zipcodes.json")),
            Specialties = jsonAssetLoader.Load<List<MedicalSpecialty>>(Path.Combine("codeset", "specialties.json"))
        };
    }

    private List<Plan> LoadPlans()
    {
        return jsonAssetLoader.Load<List<Plan>>(
            Path.Combine("referencedata", "plans.json"),
            jsonAssetLoader.CreateEnumJsonOptions());
    }

    private List<string> LoadRequiredStringList(
        string basePath,
        string fileName)
    {
        var values = jsonAssetLoader.Load<List<string>>(Path.Combine(basePath, fileName));

        if (values.Count == 0)
        {
            throw new InvalidOperationException($"Asset '{fileName}' must contain at least one value.");
        }

        return values;
    }

    private static void ValidateSettings(ProviderSearchSeedSettings settings)
    {
        if (settings.ProviderCount <= 0)
        {
            throw new InvalidOperationException("Provider seed settings must define a positive providerCount.");
        }

        if (settings.RequestingProviderPercentage <= 0 || settings.RequestingProviderPercentage >= 100)
        {
            throw new InvalidOperationException("Provider seed settings must define requestingProviderPercentage between 1 and 99.");
        }

        if (settings.MinimumLocationsPerFacility <= 0 || settings.MaximumLocationsPerFacility < settings.MinimumLocationsPerFacility)
        {
            throw new InvalidOperationException("Provider seed settings must define a valid facility location range.");
        }

        if (settings.RequestingProviderAdditionalLocationModulo <= 0)
        {
            throw new InvalidOperationException("Provider seed settings must define a positive requestingProviderAdditionalLocationModulo.");
        }

        if (settings.AllowedStates.Count == 0)
        {
            throw new InvalidOperationException("Provider seed settings must include at least one allowed state.");
        }
    }

    private static Provider CreateProvider(
        int providerIndex,
        ProviderType providerType,
        ProviderSearchSeedAssets assets)
    {
        var providerName = providerType == ProviderType.RequestingProvider
            ? $"Dr. {assets.PersonFirstNames[(providerIndex - 1) % assets.PersonFirstNames.Count]} {assets.PersonLastNames[((providerIndex - 1) * 7) % assets.PersonLastNames.Count]}"
            : $"{assets.FacilityPrefixes[(providerIndex - 1) % assets.FacilityPrefixes.Count]} {assets.FacilitySpecialties[((providerIndex - 1) * 3) % assets.FacilitySpecialties.Count]} {assets.FacilitySuffixes[((providerIndex - 1) * 5) % assets.FacilitySuffixes.Count]}";

        return new Provider
        {
            ProviderId = CreateDeterministicGuid(0x4000_0000, providerIndex, 0),
            ProviderName = providerName,
            ProviderType = providerType,
            DoingBusinessAsName = providerType == ProviderType.ServicingFacility
                ? $"{providerName} Network"
                : null,
            PhoneNumber = $"555-{300 + ((providerIndex - 1) / 100):000}-{1000 + (providerIndex % 9000):0000}",
            IsActive = true,
            EffectiveDate = assets.Settings.BaseEffectiveDate,
            TerminationDate = null
        };
    }

    private static IEnumerable<ProviderIdentifier> CreateIdentifiers(
        int providerIndex,
        Provider provider,
        DateOnly effectiveDate)
    {
        yield return new ProviderIdentifier
        {
            ProviderIdentifierId = CreateDeterministicGuid(0x5000_0000, providerIndex, 1),
            ProviderId = provider.ProviderId,
            IdentifierType = ProviderIdentifierType.TIN,
            IdentifierValue = $"{20 + (providerIndex % 70):00}-{1000000 + providerIndex:0000000}",
            IsPrimary = true,
            EffectiveDate = effectiveDate,
            TerminationDate = null
        };

        yield return new ProviderIdentifier
        {
            ProviderIdentifierId = CreateDeterministicGuid(0x5000_0000, providerIndex, 2),
            ProviderId = provider.ProviderId,
            IdentifierType = ProviderIdentifierType.NPI,
            IdentifierValue = $"1{430000000 + providerIndex:000000000}",
            IsPrimary = true,
            EffectiveDate = effectiveDate,
            TerminationDate = null
        };
    }

    private static List<ProviderLocation> CreateLocations(
        int providerIndex,
        Provider provider,
        ProviderType providerType,
        ProviderSearchSeedAssets assets,
        IReadOnlyDictionary<string, List<ZipCode>> zipCodesByState)
    {
        var locationCount = providerType == ProviderType.RequestingProvider
            ? (providerIndex % assets.Settings.RequestingProviderAdditionalLocationModulo == 0 ? 2 : 1)
            : assets.Settings.MinimumLocationsPerFacility + ((providerIndex - 1) % (assets.Settings.MaximumLocationsPerFacility - assets.Settings.MinimumLocationsPerFacility + 1));
        var locations = new List<ProviderLocation>(locationCount);

        for (var locationSequence = 1; locationSequence <= locationCount; locationSequence++)
        {
            var state = assets.Settings.AllowedStates[((providerIndex - 1) + locationSequence - 1) % assets.Settings.AllowedStates.Count];
            var zipCodes = zipCodesByState[state];
            var zipCode = zipCodes[((providerIndex - 1) * 3 + locationSequence - 1) % zipCodes.Count];
            var locationQualifier = assets.LocationQualifiers[((providerIndex - 1) + locationSequence - 1) % assets.LocationQualifiers.Count];

            locations.Add(
                new ProviderLocation
                {
                    ProviderLocationId = CreateDeterministicGuid(0x6000_0000, providerIndex, locationSequence),
                    ProviderId = provider.ProviderId,
                    LocationName = providerType == ProviderType.RequestingProvider
                        ? $"{zipCode.City} {locationQualifier} Practice"
                        : $"{zipCode.City} {locationQualifier}",
                    AddressLine1 = $"{100 + ((providerIndex * 17 + locationSequence * 13) % 9800)} {assets.StreetNames[((providerIndex - 1) * 5 + locationSequence - 1) % assets.StreetNames.Count]} {assets.StreetSuffixes[((providerIndex - 1) * 7 + locationSequence - 1) % assets.StreetSuffixes.Count]}",
                    AddressLine2 = locationSequence % 3 == 0 ? $"Suite {100 + ((providerIndex + locationSequence) % 800)}" : null,
                    City = zipCode.City,
                    StateCode = zipCode.StateCode,
                    PostalCode = zipCode.PostalCode,
                    PhoneNumber = $"555-{600 + ((providerIndex + locationSequence) % 300):000}-{1000 + ((providerIndex * 11 + locationSequence) % 9000):0000}",
                    IsActive = true,
                    EffectiveDate = assets.Settings.BaseEffectiveDate,
                    TerminationDate = null
                });
        }

        return locations;
    }

    private static IEnumerable<ProviderLocationNetwork> CreateLocationNetworks(
        int providerIndex,
        ProviderLocation location,
        ProviderType providerType,
        DateOnly effectiveDate,
        IReadOnlyDictionary<string, List<PlanNetworkSeedRecord>> planNetworksByState)
    {
        var stateNetworks = planNetworksByState[location.StateCode]
            .Select(x => x.NetworkId)
            .Distinct()
            .ToList();
        var primaryNetworkId = stateNetworks[(providerIndex - 1) % stateNetworks.Count];
        var secondaryNetworkId = stateNetworks.FirstOrDefault(networkId => networkId != primaryNetworkId);
        var shouldBeOutOfNetwork = providerType == ProviderType.ServicingFacility && providerIndex % 10 == 0 && secondaryNetworkId != Guid.Empty;

        yield return new ProviderLocationNetwork
        {
            ProviderLocationId = location.ProviderLocationId,
            NetworkId = shouldBeOutOfNetwork ? secondaryNetworkId : primaryNetworkId,
            EffectiveDate = effectiveDate,
            TerminationDate = null,
            IsPrimary = true
        };

        if (providerType == ProviderType.ServicingFacility && !shouldBeOutOfNetwork && providerIndex % 4 == 0 && secondaryNetworkId != Guid.Empty)
        {
            yield return new ProviderLocationNetwork
            {
                ProviderLocationId = location.ProviderLocationId,
                NetworkId = secondaryNetworkId,
                EffectiveDate = effectiveDate,
                TerminationDate = null,
                IsPrimary = false
            };
        }
    }

    private static Guid CreateDeterministicGuid(
        int prefix,
        int primaryIndex,
        int secondaryIndex)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..4], prefix);
        BitConverter.TryWriteBytes(bytes.Slice(4, 4), primaryIndex);
        BitConverter.TryWriteBytes(bytes.Slice(8, 4), secondaryIndex);
        BitConverter.TryWriteBytes(bytes.Slice(12, 4), prefix ^ primaryIndex ^ secondaryIndex);
        return new Guid(bytes);
    }
}
