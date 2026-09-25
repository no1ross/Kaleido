using System.ComponentModel;
using System.Reflection;
using Kaleido.Queryable.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Registry;

/// <summary>
/// Read-only directory of all queryable context types registered for this service.
/// </summary>
/// <remarks>
/// <para>
/// Populated at startup by <c>AddQueryable()</c> via assembly scanning for types
/// decorated with <c>[QueryContext]</c>. The registry is immutable after the DI
/// container is built.
/// </para>
/// <para>
/// Distinction from metadata: <c>IQueryContextRegistry</c> knows <em>which</em>
/// contexts exist (the directory); per-context field and view metadata is produced
/// separately from the registration.
/// </para>
/// </remarks>
public interface IQueryContextRegistry
{
    /// <summary>
    /// All registered query context types, in an unspecified order.
    /// </summary>
    IReadOnlyCollection<QueryContextRegistration> Registrations { get; }

    /// <summary>
    /// Returns the registration for the context with the given name, or
    /// <see langword="null"/> if no context with that name is registered.
    /// Name comparison is case-insensitive.
    /// </summary>
    QueryContextRegistration? Find(string name);

    /// <summary>
    /// Returns the registration for the context whose CLR type matches
    /// <paramref name="recordType"/>, or <see langword="null"/> if not found.
    /// </summary>
    QueryContextRegistration? Find(Type recordType);

    /// <summary>
    /// Returns the registration for the context with the given name.
    /// </summary>
    /// <exception cref="KaleidoFrameworkException">
    /// Thrown when no context with <paramref name="name"/> is registered.
    /// </exception>
    QueryContextRegistration GetRegistration(string name);

    /// <summary>
    /// Returns the registration for the context whose CLR type matches
    /// <paramref name="recordType"/>.
    /// </summary>
    /// <exception cref="KaleidoFrameworkException">
    /// Thrown when no context matching <paramref name="recordType"/> is registered.
    /// </exception>
    QueryContextRegistration GetRegistration(Type recordType);
}

internal sealed class QueryContextRegistry : IQueryContextRegistry
{
    private readonly IReadOnlyDictionary<string, QueryContextRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QueryContextRegistration> _byType;
    private readonly IReadOnlyCollection<QueryContextRegistration> _registrations;

    private readonly IDataTypeMapper _dataTypeMapper;
    private readonly IConstraintMapper _constraintMapper;

    public QueryContextRegistry(
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper,
        IServiceCollection services,
        IEnumerable<Type> contextTypes)
    {
        ArgumentNullException.ThrowIfNull(dataTypeMapper);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextTypes);

        _dataTypeMapper = dataTypeMapper;
        _constraintMapper = constraintMapper;

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
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Query context '{name}' is not registered.");
    }

    public QueryContextRegistration GetRegistration(Type contextType)
    {
        return Find(contextType)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Query context type '{contextType.FullName}' is not registered.");
    }

    private QueryContextRegistration BuildRegistration(
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
                    ConfigurationErrorCodes.QryMissingSource,
                    $"No implementation type registered for source of query context '{contextType.Name}'.");
        }

        if (allSources.Length > 1)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryDuplicateSource,
                $"Query context '{contextType.Name}' has multiple registered local sources.");
        }

        throw new KaleidoConfigurationException(
            ConfigurationErrorCodes.QryMissingSource,
            $"Query context '{contextType.Name}' does not have a registered source.");
    }

    private QueryContextMetadata BuildQueryContextMetadata(
        Type contextType)
    {
        var attribute =
            contextType.GetCustomAttribute<QueryContextAttribute>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryMissingAttribute,
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

    private FieldMetadata BuildField(
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
            _dataTypeMapper.GetDescriptor(property),
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchMode,
            sortable is not null);
    }

}
