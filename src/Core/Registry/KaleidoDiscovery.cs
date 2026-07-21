using Kaleido.Attributes;
using Kaleido.Metadata;
using Kaleido.Queryable;
using System.Reflection;

namespace Kaleido.Registry;

internal static class KaleidoDiscovery
{
    public static KaleidoDiscoveryResult Scan(
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

        return new KaleidoDiscoveryResult
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
            .Where(x =>
                x.GetCustomAttribute<KaleidoRecordAttribute>() != null)
            .Select(x =>
                new RecordDiscovery(
                    x,
                    RecordMetadataBuilder.Build(x)))
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

    private static IReadOnlyList<NamedQueryDiscovery> DiscoverNamedQueries(
        IEnumerable<Type> types)
    {
        return types
            .SelectMany(type =>
                type.GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() ==
                        typeof(IQueryableRecordNamedQuery<>))
                    .Select(i =>
                        new NamedQueryDiscovery(
                            i.GenericTypeArguments[0],
                            i,
                            type)))
            .ToList();
    }
}
