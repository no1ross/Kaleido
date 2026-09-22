using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

internal interface IQueryViewRegistrationValidator
{
    void Validate(
        IReadOnlyCollection<Type> queryViewTypes,
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services);
}

internal sealed class QueryViewRegistrationValidator
    : IQueryViewRegistrationValidator
{
    public void Validate(
        IReadOnlyCollection<Type> queryViewTypes,
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(queryViewTypes);
        ArgumentNullException.ThrowIfNull(queryContextTypes);
        ArgumentNullException.ThrowIfNull(services);

        ValidateDuplicateQueryViewNames(
            queryViewTypes);

        ValidateQueryViewInterfaces(
            queryViewTypes);

        ValidateQueryRegistrations(
            queryViewTypes,
            queryContextTypes);
    }

    private static void ValidateDuplicateQueryViewNames(
        IReadOnlyCollection<Type> queryViewTypes)
    {
        var duplicates =
            queryViewTypes
                .Select(x => new
                {
                    Type = x,
                    Attribute =
                        x.GetCustomAttribute<QueryViewAttribute>()
                })
                .GroupBy(
                    x => x.Attribute?.Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToArray();

        if (duplicates.Length == 0)
        {
            return;
        }

        throw new KaleidoConfigurationException(
            $"Duplicate query view names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }

    private static void ValidateQueryViewInterfaces(
        IReadOnlyCollection<Type> queryViewTypes)
    {
        foreach (var queryViewType in queryViewTypes)
        {
            var syncInterfaces =
                queryViewType
                    .GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        (
                            i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,>) ||
                            i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,,>)
                        ))
                    .ToArray();

            var asyncInterfaces =
                queryViewType
                    .GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        (
                            i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,>) ||
                            i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,,>)
                        ))
                    .ToArray();

            if (syncInterfaces.Length == 0 && asyncInterfaces.Length == 0)
            {
                throw new KaleidoConfigurationException(
                    $"Query view '{queryViewType.Name}' must implement IQueryViewSource or IQueryViewSourceAsync.");
            }

            if (syncInterfaces.Length > 0 && asyncInterfaces.Length > 0)
            {
                throw new KaleidoConfigurationException(
                    $"Query view '{queryViewType.Name}' implements both IQueryViewSource and IQueryViewSourceAsync. " +
                    $"Implement exactly one.");
            }
        }
    }

    private static void ValidateQueryRegistrations(
        IReadOnlyCollection<Type> queryViewTypes,
        IReadOnlyCollection<Type> queryContextTypes)
    {
        var registeredContexts =
            queryContextTypes.ToHashSet();

        foreach (var queryViewType in queryViewTypes)
        {
            var queryViewInterface =
                GetQueryViewInterface(
                    queryViewType);

            var contextType =
                queryViewInterface.GenericTypeArguments[0];

            if (!registeredContexts.Contains(contextType))
            {
                throw new KaleidoConfigurationException(
                    $"Query view '{queryViewType.Name}' references unregistered query context '{contextType.Name}'.");
            }

            var contractType =
                queryViewInterface.GenericTypeArguments[1];

            if (!contractType.IsClass)
            {
                throw new KaleidoConfigurationException(
                    $"Query view '{queryViewType.Name}' references invalid contract type '{contractType.Name}'.");
            }
        }
    }

    private static Type GetQueryViewInterface(
        Type queryViewType)
    {
        return queryViewType
            .GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                (
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,,>)
                ))
            .OrderByDescending(
                i => i.GenericTypeArguments.Length)
            .First();
    }
}