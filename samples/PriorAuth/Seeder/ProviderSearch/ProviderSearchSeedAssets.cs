using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;

namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch;

internal sealed class ProviderSearchSeedAssets
{
    public required List<Provider> Providers { get; init; }

    public required List<ProviderIdentifier> ProviderIdentifiers { get; init; }

    public required List<ProviderLocation> ProviderLocations { get; init; }

    public required List<ProviderLocationNetwork> ProviderLocationNetworks { get; init; }

    public required List<ProviderLocationSpecialtyAsset> ProviderLocationSpecialties { get; init; }
}

internal sealed class ProviderLocationSpecialtyAsset
{
    public required Guid ProviderLocationId { get; init; }

    public required string SpecialtyCode { get; init; }

    public bool IsPrimary { get; init; }

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }
}
