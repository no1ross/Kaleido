using Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;
using Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.ProviderSearch.ReferenceData;

namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch;

internal sealed class ProviderSearchSeedAssets
{
    public required List<string> PersonFirstNames { get; init; }

    public required List<string> PersonLastNames { get; init; }

    public required List<string> FacilityPrefixes { get; init; }

    public required List<string> FacilitySpecialties { get; init; }

    public required List<string> FacilitySuffixes { get; init; }

    public required List<string> StreetNames { get; init; }

    public required List<string> StreetSuffixes { get; init; }

    public required List<string> LocationQualifiers { get; init; }

    public required ProviderSearchSeedSettings Settings { get; init; }

    public required List<PlanNetworkSeedRecord> PlanNetworks { get; init; }

    public required List<ZipCode> ZipCodes { get; init; }

    public required List<MedicalSpecialty> Specialties { get; init; }
}
