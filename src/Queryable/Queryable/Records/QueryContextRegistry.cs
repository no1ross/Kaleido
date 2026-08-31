using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido.Queryable.Records;

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
                contextType,
                sourceType);

        return new QueryContextRegistration(
            contextType,
            sourceType,
            metadata);
    }

    private static Type GetSourceRegistration(
        IServiceCollection services,
        Type contextType)
    {
        var localSourceInterface =
            typeof(IQueryContextSource<>)
                .MakeGenericType(contextType);

        var localSources =
            services
                .Where(x => x.ServiceType == localSourceInterface)
                .ToArray();

        var delegatedSources =
            services
                .Where(x =>
                    x.ServiceType.IsGenericType &&
                    x.ServiceType.GetGenericTypeDefinition() == typeof(IDelegatedQueryContextSource<,>) &&
                    x.ServiceType.GenericTypeArguments[0] == contextType)
                .ToArray();

        if (localSources.Length > 0 && delegatedSources.Length > 0)
        {
            throw new InvalidOperationException(
                $"Query context '{contextType.Name}' cannot register both local and delegated sources.");
        }

        if (localSources.Length == 1)
        {
            return localSources[0].ImplementationType
                ?? throw new InvalidOperationException(
                    $"No implementation type registered for source '{localSourceInterface.Name}'.");
        }

        if (delegatedSources.Length == 1)
        {
            return delegatedSources[0].ImplementationType
                ?? throw new InvalidOperationException(
                    $"No implementation type registered for delegated source '{delegatedSources[0].ServiceType.Name}'.");
        }

        if (localSources.Length > 1)
        {
            throw new InvalidOperationException(
                $"Query context '{contextType.Name}' has multiple registered local sources.");
        }

        if (delegatedSources.Length > 1)
        {
            throw new InvalidOperationException(
                $"Query context '{contextType.Name}' has multiple registered delegated sources.");
        }

        throw new InvalidOperationException(
            $"Query context '{contextType.Name}' does not have a registered source.");
    }

    private static QueryContextMetadata BuildQueryContextMetadata(
        Type contextType,
        Type sourceType)
    {
        var attribute =
            contextType.GetCustomAttribute<QueryContextAttribute>()
            ?? throw new InvalidOperationException(
                $"Query context '{contextType.Name}' is missing QueryContextAttribute.");

        ValidateSourceKindCompatibility(
            contextType,
            sourceType,
            attribute.Kind);

        var pageable =
            attribute.Kind is QueryContextKind.Direct or QueryContextKind.Delegated
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

    private static void ValidateSourceKindCompatibility(
        Type contextType,
        Type sourceType,
        QueryContextKind kind)
    {
        var isDelegated =
            sourceType
                .GetInterfaces()
                .Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IDelegatedQueryContextSource<,>) &&
                    i.GenericTypeArguments[0] == contextType);

        if (kind == QueryContextKind.Delegated && !isDelegated)
        {
            throw new InvalidOperationException(
                $"Query context '{contextType.Name}' is marked Delegated but source '{sourceType.Name}' is not delegated.");
        }

        if (kind != QueryContextKind.Delegated && isDelegated)
        {
            throw new InvalidOperationException(
                $"Query context '{contextType.Name}' uses delegated source '{sourceType.Name}' but is marked '{kind}'.");
        }
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
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchMode,
            sortable is not null);
    }

}