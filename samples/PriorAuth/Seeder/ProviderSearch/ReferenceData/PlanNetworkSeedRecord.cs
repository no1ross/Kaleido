namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch.ReferenceData;

internal sealed class PlanNetworkSeedRecord
{
    public required string PlanId { get; init; }

    public required Guid NetworkId { get; init; }

    public required DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }

    public bool IsPrimary { get; init; }
}
