using System.Reflection;
using Kaleido.Queryable.Metadata;
using Kaleido.Registry;

namespace Kaleido.Queryable.Registry;

internal interface IQuerySourceRegistry
{
    IReadOnlyCollection<QuerySourceRegistration> Registrations { get; }

    QuerySourceRegistration? Find(string name);

    QuerySourceRegistration? Find(Type sourceType);

    QuerySourceRegistration GetRegistration(string name);

    QuerySourceRegistration GetRegistration(Type sourceType);
}

/// <summary>
/// The discovered query sources — local and delegated — keyed by source type and by name
/// (the source's type name, case-insensitive).
/// </summary>
internal sealed class QuerySourceRegistry : IQuerySourceRegistry
{
    private readonly IReadOnlyDictionary<string, QuerySourceRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QuerySourceRegistration> _byType;
    private readonly IReadOnlyCollection<QuerySourceRegistration> _registrations;

    private readonly ITypeDescriber _typeDescriber;
    private readonly IConstraintMapper _constraintMapper;
    private readonly AuthorizationMetadata _defaultAuthorization;

    public QuerySourceRegistry(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        IEnumerable<Type> localSourceTypes,
        IEnumerable<Type> delegatedSourceTypes,
        AuthorizationMetadata defaultAuthorization)
    {
        ArgumentNullException.ThrowIfNull(typeDescriber);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(localSourceTypes);
        ArgumentNullException.ThrowIfNull(delegatedSourceTypes);
        ArgumentNullException.ThrowIfNull(defaultAuthorization);

        _typeDescriber = typeDescriber;
        _constraintMapper = constraintMapper;
        _defaultAuthorization = defaultAuthorization;

        var registrations =
            localSourceTypes
                .Select(BuildLocalRegistration)
                .Concat(delegatedSourceTypes.Select(BuildDelegatedRegistration))
                .ToArray();

        _registrations = registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.SourceType);
    }

    public IReadOnlyCollection<QuerySourceRegistration> Registrations =>
        _registrations;

    public QuerySourceRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public QuerySourceRegistration? Find(Type sourceType)
    {
        _byType.TryGetValue(
            sourceType,
            out var registration);

        return registration;
    }

    public QuerySourceRegistration GetRegistration(string name) =>
        Find(name)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Query source '{name}' is not registered.");

    public QuerySourceRegistration GetRegistration(Type sourceType) =>
        Find(sourceType)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Query source type '{sourceType.FullName}' is not registered.");

    private QuerySourceRegistration BuildLocalRegistration(
        Type sourceType)
    {
        var contextType =
            sourceType.GetLocalSourceMarkerInterfaces()
                .Single()
                .GenericTypeArguments[0];

        return new QuerySourceRegistration(
            sourceType,
            contextType,
            contextType,
            typeof(EmptyQueryViewParameters),
            BuildMetadata(
                sourceType,
                contextType,
                contextType,
                typeof(EmptyQueryViewParameters),
                QuerySourceKind.Local));
    }

    private QuerySourceRegistration BuildDelegatedRegistration(
        Type sourceType)
    {
        var arguments =
            sourceType.GetDelegatedSourceInterfaces()
                .Single()
                .GenericTypeArguments;

        return new QuerySourceRegistration(
            sourceType,
            arguments[0],
            arguments[1],
            arguments[2],
            BuildMetadata(
                sourceType,
                arguments[0],
                arguments[1],
                arguments[2],
                QuerySourceKind.Delegated));
    }

    private QuerySourceMetadata BuildMetadata(
        Type sourceType,
        Type contextType,
        Type resultType,
        Type parametersType,
        QuerySourceKind kind)
    {
        var attribute =
            sourceType.GetCustomAttribute<QuerySourceAttribute>()
            ?? throw new KaleidoConfigurationException(
                QueryableErrorCodes.MissingAttribute,
                $"Query source '{sourceType.FullName}' must be decorated with [QuerySource].");

        var pageable =
            sourceType.GetCustomAttribute<PageableAttribute>() is { } pageableAttribute
                ? new PageableMetadata(pageableAttribute.DefaultSize, pageableAttribute.MaxSize)
                : null;

        return new QuerySourceMetadata(
            sourceType.Name,
            attribute.Description,
            attribute.DisplayName,
            attribute.Version,
            attribute.Source,
            kind,
            pageable,
            contextType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.ToFieldMetadata(_typeDescriber))
                .ToArray(),
            parametersType.ToParameterMetadata(_typeDescriber, _constraintMapper),
            resultType.ToOutputFieldMetadata(_typeDescriber),
            AuthorizationMetadata.ForType(sourceType, _defaultAuthorization));
    }
}
