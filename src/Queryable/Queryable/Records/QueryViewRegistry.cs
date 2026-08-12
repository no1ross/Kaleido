using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

internal sealed class QueryViewRegistry
    : IQueryViewRegistry
{
    private readonly IReadOnlyDictionary<string, QueryViewRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QueryViewRegistration> _byType;
    private readonly IReadOnlyCollection<QueryViewRegistration> _registrations;

    public QueryViewRegistry(
        IServiceCollection services,
        IEnumerable<Type> queryViewTypes)
    {
        var registrations =
            queryViewTypes
                .Select(x =>
                    BuildRegistration(x))
                .ToArray();

        _registrations =
            registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.QueryViewType);
    }

    public IReadOnlyCollection<QueryViewRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<QueryViewRegistration> GetAll() =>
        _registrations;

    public QueryViewRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public QueryViewRegistration? Find(Type queryViewType)
    {
        _byType.TryGetValue(
            queryViewType,
            out var registration);

        return registration;
    }

    public QueryViewRegistration GetRegistration(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Query view '{name}' is not registered.");
    }

    public QueryViewRegistration GetRegistration(Type queryViewType)
    {
        return Find(queryViewType)
            ?? throw new KeyNotFoundException(
                $"Query view '{queryViewType.FullName}' is not registered.");
    }

    private static QueryViewRegistration BuildRegistration(
        Type queryViewType)
    {
        var queryViewAttribute =
            queryViewType.GetCustomAttribute<QueryViewAttribute>()
            ?? throw new InvalidOperationException(
                $"Query view '{queryViewType.Name}' is missing QueryViewAttribute.");

        var queryViewInterface =
            GetQueryViewInterface(
                queryViewType);

        var contextType =
            queryViewInterface.GenericTypeArguments[0];

        var sourceType =
            queryViewInterface.GenericTypeArguments[1];

        var parametersType =
            queryViewInterface.GenericTypeArguments.Length == 3
                ? queryViewInterface.GenericTypeArguments[2]
                : typeof(EmptyQueryViewParameters);

        return new QueryViewRegistration(
            queryViewType,
            sourceType,
            parametersType,
            contextType,
            new QueryViewMetadata(
                queryViewAttribute.Name,
                queryViewAttribute.Version,
                queryViewAttribute.DisplayName ?? queryViewAttribute.Name,
                queryViewAttribute.Description
                    ?? queryViewAttribute.DisplayName
                    ?? queryViewAttribute.Name,
                BuildPageable(queryViewType),
                BuildParameters(parametersType)));
    }

    private static IReadOnlyList<QueryParameterMetadata> BuildParameters(
        Type parametersType)
    {
        if (parametersType == typeof(EmptyQueryViewParameters))
        {
            return Array.Empty<QueryParameterMetadata>();
        }

        return parametersType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryParameterMetadata(
                    property.Name,
                    property.PropertyType,
                    property.GetCustomAttribute<RequiredAttribute>() is not null,
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    property.GetCustomAttribute<DefaultValueAttribute>()?.Value))
            .ToArray();
    }

    private static PageableMetadata? BuildPageable(
        Type queryViewType)
    {
        var pageable =
            queryViewType.GetCustomAttribute<PageableAttribute>();

        if (pageable is null)
        {
            return null;
        }

        return new PageableMetadata(
            pageable.DefaultSize,
            pageable.MaxSize);
    }

    private static Type GetQueryViewInterface(
        Type queryViewType)
    {
        return queryViewType
            .GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                (
                    i.GetGenericTypeDefinition() ==
                        typeof(IQueryViewSource<,>) ||

                    i.GetGenericTypeDefinition() ==
                        typeof(IQueryViewSource<,,>)
                ))
            .OrderByDescending(
                i => i.GenericTypeArguments.Length)
            .First();
    }

}
