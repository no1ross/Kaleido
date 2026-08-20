using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
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
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<ProviderSearchDbContext>(
                serviceProjectName: "ProviderSearch",
                connectionStringName: "ProviderSearch",
                fallbackConnectionString: "Data Source=data/providersearch.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ProviderSearchDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();
        var specialtyMap = LoadSpecialtyMap();

        dbContext.Providers.AddRange(assets.Providers);
        dbContext.ProviderIdentifiers.AddRange(assets.ProviderIdentifiers);
        dbContext.ProviderLocations.AddRange(assets.ProviderLocations);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ProviderLocationNetworks.AddRange(assets.ProviderLocationNetworks);
        dbContext.ProviderLocationSpecialties.AddRange(
            assets.ProviderLocationSpecialties.Select(x =>
                new ProviderLocationSpecialty
                {
                    ProviderLocationId = x.ProviderLocationId,
                    MedicalSpecialtyId = ResolveSpecialtyId(x.SpecialtyCode, specialtyMap),
                    IsPrimary = x.IsPrimary,
                    EffectiveDate = x.EffectiveDate,
                    TerminationDate = x.TerminationDate
                }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private ProviderSearchSeedAssets LoadAssets()
    {
        const string basePath = "providersearch";
        var enumOptions = jsonAssetLoader.CreateEnumJsonOptions();

        return new ProviderSearchSeedAssets
        {
            Providers = jsonAssetLoader.Load<List<Provider>>(Path.Combine(basePath, "providers.json")),
            ProviderIdentifiers = jsonAssetLoader.Load<List<ProviderIdentifier>>(Path.Combine(basePath, "provider-identifiers.json"), enumOptions),
            ProviderLocations = jsonAssetLoader.Load<List<ProviderLocation>>(Path.Combine(basePath, "provider-locations.json")),
            ProviderLocationNetworks = jsonAssetLoader.Load<List<ProviderLocationNetwork>>(Path.Combine(basePath, "provider-location-networks.json")),
            ProviderLocationSpecialties = jsonAssetLoader.Load<List<ProviderLocationSpecialtyAsset>>(Path.Combine(basePath, "provider-location-specialties.json"))
        };
    }

    private Dictionary<string, Guid> LoadSpecialtyMap()
    {
        var specialties =
            jsonAssetLoader.Load<List<MedicalSpecialty>>(
                Path.Combine("codeset", "specialties.json"));

        return specialties.ToDictionary(
            x => x.SpecialtyCode,
            x => x.MedicalSpecialtyId,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Guid ResolveSpecialtyId(
        string specialtyCode,
        IReadOnlyDictionary<string, Guid> specialtyMap)
    {
        if (specialtyMap.TryGetValue(specialtyCode, out var specialtyId))
        {
            return specialtyId;
        }

        throw new InvalidOperationException($"Specialty '{specialtyCode}' was not found in the code set seed assets.");
    }
}
