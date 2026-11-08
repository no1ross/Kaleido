namespace Kaleido.Server.Registry;

// The query sources a step's lookup may name (#524), found in the central registry's full copy.
internal sealed class LookupSources(IReadOnlyCollection<QueryableSourceResponse> known)
{
    public Task<QueryableSourceResponse?> FindAsync(ProcessFieldLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Find(known, lookup));
    }

    private static QueryableSourceResponse? Find(IReadOnlyCollection<QueryableSourceResponse> queryables, ProcessFieldLookup lookup) =>
        queryables.FirstOrDefault(x =>
            string.Equals(x.ServiceName, lookup.Service, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Name, lookup.Source, StringComparison.OrdinalIgnoreCase));
}
