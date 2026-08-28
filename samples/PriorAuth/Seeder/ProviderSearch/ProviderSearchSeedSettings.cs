namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch;

internal sealed class ProviderSearchSeedSettings
{
    public int ProviderCount { get; set; }

    public int RequestingProviderPercentage { get; set; }

    public int MinimumLocationsPerFacility { get; set; }

    public int MaximumLocationsPerFacility { get; set; }

    public int RequestingProviderAdditionalLocationModulo { get; set; }

    public DateOnly BaseEffectiveDate { get; set; }

    public List<string> AllowedStates { get; set; } = [];
}
