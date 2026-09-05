namespace Kaleido.Samples.PriorAuth.ProviderSearch.Models;

public sealed record QueryableResult<TView>
{
    public IReadOnlyCollection<TView> Results { get; init; } = [];
}
