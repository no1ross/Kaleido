using Kaleido.Exceptions;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido.Queryable.Records;

/// <summary>
/// Maintains the list of registered records available
/// to the application.
///
/// While IRecordMetadataCatalog is responsible for generating
/// metadata for a specific record type, the registry is responsible
/// for discovering which records exist within the application.
///
/// Think of this component as the directory of available records.
/// </summary>
///
/// <remarks>
/// MetadataCatalog = describes one record
/// Registry = knows all records
/// </remarks>
public interface IQueryContextRegistry
{
    IReadOnlyCollection<QueryContextRegistration> Registrations { get; }

    QueryContextRegistration? Find(string name);

    QueryContextRegistration? Find(Type recordType);

    QueryContextRegistration GetRegistration(string name);

    QueryContextRegistration GetRegistration(Type recordType);
}

internal sealed class QueryContextRegistry : IQueryContextRegistry
{
    private readonly IReadOnlyDictionary<string, QueryContextRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QueryContextRegistration> _byType;
    private readonly IReadOnlyCollection<QueryContextRegistration> _registrations;

    public QueryContextRegistry(
        IServiceCollection services,
        IEnumerable<Type> contextTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextTypes);

        var registrations =
            contextTypes
                .Select(contextType =>
                    BuildRegistration(
                        services,
                        contextType))
                .ToArray();

        _registrations = registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.ContextType);
    }

    public IReadOnlyCollection<QueryContextRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<QueryContextRegistration> GetAll() =>
        _registrations;

    public QueryContextRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public QueryContextRegistration? Find(Type contextType)
    {
        _byType.TryGetValue(
            contextType,
            out var registration);

        return registration;
    }

    public QueryContextRegistration GetRegistration(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Query context '{name}' is not registered.");
    }

    public QueryContextRegistration GetRegistration(Type contextType)
    {
        return Find(contextType)
            ?? throw new KeyNotFoundException(
                $"Query context type '{contextType.FullName}' is not registered.");
    }

    private static QueryContextRegistration BuildRegistration(
        IServiceCollection services,
        Type contextType)
    {
        var sourceType =
            GetSourceRegistration(
                services,
                contextType);

        var metadata =
            BuildQueryContextMetadata(
                contextType);

        return new QueryContextRegistration(
            contextType,
            sourceType,
            metadata);
    }

    private static Type GetSourceRegistration(
        IServiceCollection services,
        Type contextType)
    {
        var syncInterface =
            typeof(IQueryContextSource<>)
                .MakeGenericType(contextType);

        var asyncInterface =
            typeof(IQueryContextSourceAsync<>)
                .MakeGenericType(contextType);

        var syncSources =
            services
                .Where(x => x.ServiceType == syncInterface)
                .ToArray();

        var asyncSources =
            services
                .Where(x => x.ServiceType == asyncInterface)
                .ToArray();

        var allSources = syncSources.Concat(asyncSources).ToArray();

        if (allSources.Length == 1)
        {
            return allSources[0].ImplementationType
                ?? throw new KaleidoConfigurationException(
                    $"No implementation type registered for source of query context '{contextType.Name}'.");
        }

        if (allSources.Length > 1)
        {
            throw new KaleidoConfigurationException(
                $"Query context '{contextType.Name}' has multiple registered local sources.");
        }

        throw new KaleidoConfigurationException(
            $"Query context '{contextType.Name}' does not have a registered source.");
    }

    private static QueryContextMetadata BuildQueryContextMetadata(
        Type contextType)
    {
        var attribute =
            contextType.GetCustomAttribute<QueryContextAttribute>()
            ?? throw new KaleidoConfigurationException(
                $"Query context '{contextType.Name}' is missing QueryContextAttribute.");

        var pageable =
            attribute.Kind == QueryContextKind.Direct
                ? BuildPageable(contextType)
                : null;

        return new QueryContextMetadata(
            attribute.Name,
            attribute.Description ?? attribute.DisplayName ?? attribute.Name,
            attribute.DisplayName ?? attribute.Name,
            attribute.Version,
            attribute.Source,
            attribute.Kind,
            pageable,
            contextType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(BuildField)
                .ToArray());
    }

    private static PageableMetadata? BuildPageable(
        Type contextType)
    {
        var pageable =
            contextType.GetCustomAttribute<PageableAttribute>();

        if (pageable is null)
        {
            return null;
        }

        return new PageableMetadata(
            pageable.DefaultSize,
            pageable.MaxSize);
    }

    private static FieldMetadata BuildField(
        PropertyInfo property)
    {
        var filterable =
            property.GetCustomAttribute<FilterableAttribute>();

        var searchable =
            property.GetCustomAttribute<SearchableAttribute>();

        var sortable =
            property.GetCustomAttribute<SortableAttribute>();

        var description =
            property.GetCustomAttribute<DescriptionAttribute>();

        return new FieldMetadata(
            property.Name,
            description?.Description,
            property.PropertyType,
            DataTypeMapper.GetDescriptor(property),
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchMode,
            sortable is not null);
    }

}