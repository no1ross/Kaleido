using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

public interface IQueryViewRegistrationValidator
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
                    x => x.Attribute!.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToArray();

        if (duplicates.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Duplicate query view names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }

    private static void ValidateQueryViewInterfaces(
        IReadOnlyCollection<Type> queryViewTypes)
    {
        foreach (var queryViewType in queryViewTypes)
        {
            var interfaces =
                queryViewType
                    .GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        (
                            i.GetGenericTypeDefinition() ==
                            typeof(IQueryViewSource<,>) ||

                            i.GetGenericTypeDefinition() ==
                            typeof(IQueryViewSource<,,>)
                        ))
                    .ToArray();

            if (interfaces.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Query view '{queryViewType.Name}' must implement IQueryViewSource.");
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
                throw new InvalidOperationException(
                    $"Query view '{queryViewType.Name}' references unregistered query context '{contextType.Name}'.");
            }

            var contractType =
                queryViewInterface.GenericTypeArguments[1];

            if (!contractType.IsClass)
            {
                throw new InvalidOperationException(
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