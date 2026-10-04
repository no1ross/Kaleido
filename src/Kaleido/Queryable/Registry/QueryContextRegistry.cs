using System.Reflection;
using Kaleido.Queryable.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Registry;

internal interface IQueryContextRegistry
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

    private readonly ITypeDescriber _dataTypeMapper;
    private readonly IConstraintMapper _constraintMapper;
    private readonly AuthorizationMetadata _defaultAuthorization;

    public QueryContextRegistry(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        IServiceCollection services,
        IEnumerable<Type> contextTypes)
        : this(typeDescriber, constraintMapper, services, contextTypes, AuthorizationMetadata.Unspecified)
    {
    }

    public QueryContextRegistry(
        ITypeDescriber TypeDescriber,
        IConstraintMapper constraintMapper,
        IServiceCollection services,
        IEnumerable<Type> contextTypes,
        AuthorizationMetadata defaultAuthorization)
    {
        ArgumentNullException.ThrowIfNull(TypeDescriber);
        ArgumentNullException.ThrowIfNull(defaultAuthorization);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextTypes);

        _dataTypeMapper = TypeDescriber;
        _constraintMapper = constraintMapper;
        _defaultAuthorization = defaultAuthorization;

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
                .Select(x => x.ToFieldMetadata(_dataTypeMapper))
                .ToArray(),
            AuthorizationMetadata.ForType(contextType, _defaultAuthorization));
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
}
