using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using System.Reflection;

namespace Kaleido.Queryable.Registry;

public static class QueryableDiscovery
{
    public static RecordDiscoveryResult Scan(
        IEnumerable<Assembly> assemblies)
    {
        var types = assemblies
            .Distinct()
            .SelectMany(x => x.DefinedTypes)
            .Where(x =>
                x.IsClass &&
                !x.IsAbstract)
            .Select(x => x.AsType())
            .ToList();

        return new RecordDiscoveryResult
        {
            Records = DiscoverRecords(types),
            Sources = DiscoverSources(types),
            NamedQueries = DiscoverNamedQueries(types)
        };
    }

    private static IReadOnlyList<RecordDiscovery> DiscoverRecords(
        IEnumerable<Type> types)
    {
        return types
            .Select(type => new
            {
                Type = type,
                Attribute = type.GetCustomAttribute<KaleidoRecordAttribute>()
            })
            .Where(x => x.Attribute is not null)
            .Select(x => new RecordDiscovery(
                x.Type,
                x.Attribute!.Name,
                x.Attribute.Description,
                x.Attribute.Version,
                x.Attribute.Source,
                x.Type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(BuildField)
                    .ToArray(),
                BuildPageable(x.Type)))
            .ToList();
    }

    private static IReadOnlyList<SourceDiscovery> DiscoverSources(
        IEnumerable<Type> types)
    {
        return types
            .SelectMany(type =>
                type.GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() ==
                        typeof(IQueryableRecordSource<>))
                    .Select(i =>
                        new SourceDiscovery(
                            i.GenericTypeArguments[0],
                            i,
                            type)))
            .ToList();
    }

    private static IReadOnlyList<NamedQueryDiscovery> DiscoverNamedQueries(IEnumerable<Type> types)
    {
        var results = new List<NamedQueryDiscovery>();

        foreach (var type in types)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType ||
                    iface.GetGenericTypeDefinition() !=
                    typeof(IQueryableRecordNamedQuery<>))
                {
                    continue;
                }

                var instance =
                    Activator.CreateInstance(type);

                dynamic query = instance!;

                results.Add(
                    new NamedQueryDiscovery(
                        iface.GenericTypeArguments[0],
                        iface,
                        type,
                        query.Name,
                        query.Description,
                        query.Parameters));
            }
        }

        return results;
    }

    private static FieldMetadata BuildField(PropertyInfo property)
    {
        var filterable = property.GetCustomAttribute<FilterableAttribute>();
        var searchable = property.GetCustomAttribute<SearchableAttribute>();
        var sortable = property.GetCustomAttribute<SortableAttribute>();

        return new FieldMetadata(
            property.Name,
            property.PropertyType,
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchModes ?? Array.Empty<MatchMode>(),
            sortable is not null);
    }

    private static PageableMetadata? BuildPageable(Type recordType)
    {
        var pageable = recordType.GetCustomAttribute<PageableAttribute>();
        if (pageable is null)
        {
            return null;
        }
        return new PageableMetadata(
            pageable.DefaultSize,
            pageable.MaxSize);
    }
}
