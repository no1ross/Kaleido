namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Models;

public sealed record PlanNetworkRecord
{
    public string PlanId { get; init; } = string.Empty;

    public Guid[] NetworkIds { get; init; } = [];
}
