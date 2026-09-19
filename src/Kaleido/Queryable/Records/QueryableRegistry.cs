using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Records;

internal sealed class QueryableRegistry : IQueryableRegistry
{
    private readonly IReadOnlyCollection<QueryableContextRegistryItem> _registrations;
    private readonly IReadOnlyDictionary<string, QueryableContextRegistryItem> _byName;

    public QueryableRegistry(
        IQueryContextRegistry contextRegistry,
        IQueryViewRegistry viewRegistry,
        IDelegatedQueryViewRegistry delegatedViewRegistry)
    {
        ArgumentNullException.ThrowIfNull(contextRegistry);
        ArgumentNullException.ThrowIfNull(viewRegistry);
        ArgumentNullException.ThrowIfNull(delegatedViewRegistry);

        var localRegistrations =
            contextRegistry.Registrations
                .Select(context =>
                    QueryableRegistryProjection.Project(
                        context,
                        viewRegistry.Registrations
                            .Where(view => view.QueryContextType == context.ContextType)
                            .ToArray()));

        var delegatedRegistrations =
            delegatedViewRegistry.Registrations
                .GroupBy(x => x.QueryMetadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    QueryableRegistryProjection.Project(
                        group.First().QueryMetadata,
                        group.ToArray()));

        _registrations =
            localRegistrations
                .Concat(delegatedRegistrations)
                .OrderBy(x => x.Name)
                .ToArray();

        _byName =
            _registrations.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<QueryableContextRegistryItem> Registrations =>
        _registrations;

    public QueryableContextRegistryItem? Find(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _byName.TryGetValue(name, out var registration);
        return registration;
    }

    public QueryableContextRegistryItem GetRegistration(
        string name) =>
        Find(name)
        ?? throw new KeyNotFoundException(
            $"Queryable registry item '{name}' is not registered.");
}
