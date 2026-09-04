namespace Kaleido.Samples.PriorAuth.ProviderSearch.Models;

public sealed record QueryableResult<TRecord>
{
    public IReadOnlyCollection<TRecord> Records { get; init; } = [];
}
