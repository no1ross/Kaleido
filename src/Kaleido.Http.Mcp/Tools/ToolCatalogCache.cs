namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// The tool catalogs one MCP registration has translated, by registry revision (#464). Owned by the
/// host's container (a singleton of <c>AddMcp()</c>), so hosts in one process never share one, and
/// bounded: past <see cref="ToolCatalogCache.Capacity"/> the least recently used catalog is dropped.
/// </summary>
internal interface IToolCatalogCache
{
    /// <summary>The catalog translated for <paramref name="key"/>, which becomes the most recently used.</summary>
    bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ToolCatalog? catalog);

    /// <summary>Keeps <paramref name="catalog"/> for <paramref name="key"/>, dropping the least recently used one when full.</summary>
    void Add(string key, ToolCatalog catalog);
}

internal sealed class ToolCatalogCache : IToolCatalogCache
{
    // One per distinct caller-visible registry: far more than the roles a host serves.
    internal const int Capacity = 256;

    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, ToolCatalog Catalog)>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, ToolCatalog Catalog)> _recency = new();

    /// <summary>How many catalogs are kept.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ToolCatalog? catalog)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);
                catalog = node.Value.Catalog;
                return true;
            }
        }

        catalog = null;
        return false;
    }

    public void Add(string key, ToolCatalog catalog)
    {
        lock (_gate)
        {
            if (_entries.Remove(key, out var known))
            {
                _recency.Remove(known);
            }
            else if (_entries.Count >= Capacity && _recency.Last is { } oldest)
            {
                _entries.Remove(oldest.Value.Key);
                _recency.RemoveLast();
            }

            _entries[key] = _recency.AddFirst((key, catalog));
        }
    }
}
