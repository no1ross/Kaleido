namespace Kaleido.Http.Registry;

// The query sources a step's lookup may name (#524), found in the full registry: the registry being
// filtered when it already is the full one (an aggregating host), else the registry source's, read once
// and only if a lookup is not found among this host's own.
internal sealed class LookupSources(
    IReadOnlyCollection<QueryableSourceResponse> known,
    bool complete,
    IKaleidoRegistrySource source,
    int? maxParallelism)
{
    private IReadOnlyCollection<QueryableSourceResponse>? _others;

    public async Task<QueryableSourceResponse?> FindAsync(ProcessFieldLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var found = Find(known, lookup);

        if (found is not null || complete)
        {
            return found;
        }

        _others ??= (await source.GetAsync(maxParallelism, cancellationToken)).Queryables;

        return Find(_others, lookup);
    }

    private static QueryableSourceResponse? Find(IReadOnlyCollection<QueryableSourceResponse> queryables, ProcessFieldLookup lookup) =>
        queryables.FirstOrDefault(x =>
            string.Equals(x.ServiceName, lookup.Service, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Name, lookup.Source, StringComparison.OrdinalIgnoreCase));
}
