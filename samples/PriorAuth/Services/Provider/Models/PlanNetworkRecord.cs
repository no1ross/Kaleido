namespace Kaleido.Samples.PriorAuth.ProviderSearch.Models;

public sealed record PlanNetworkRecord
{
    public string PlanId { get; init; } = string.Empty;

    public Guid NetworkId { get; init; }
}
