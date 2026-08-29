namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Models;

public sealed record PlanNetworkRecord
{
    public string PlanId { get; init; } = string.Empty;

    public Guid[] NetworkIds { get; init; } = [];
}
