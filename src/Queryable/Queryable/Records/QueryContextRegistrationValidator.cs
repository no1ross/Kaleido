using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

internal sealed class QueryContextRegistrationValidator
    : IQueryContextRegistrationValidator
{
    public void Validate(
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(queryContextTypes);
        ArgumentNullException.ThrowIfNull(services);

        ValidateDuplicateQueryContextNames(
            queryContextTypes);

        ValidateQueryContextSources(
            queryContextTypes,
            services);
    }

    private static void ValidateDuplicateQueryContextNames(
        IReadOnlyCollection<Type> queryContextTypes)
    {
        var duplicates =
            queryContextTypes
                .Select(x => new
                {
                    Type = x,
                    Attribute =
                        x.GetCustomAttribute<QueryContextAttribute>()
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
            $"Duplicate query context names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }

    private static void ValidateQueryContextSources(
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services)
    {
        foreach (var queryContextType in queryContextTypes)
        {
            var syncInterface =
                typeof(IQueryContextSource<>)
                    .MakeGenericType(queryContextType);

            var asyncInterface =
                typeof(IQueryContextSourceAsync<>)
                    .MakeGenericType(queryContextType);

            var syncCount =
                services.Count(x => x.ServiceType == syncInterface);

            var asyncCount =
                services.Count(x => x.ServiceType == asyncInterface);

            if (syncCount == 0 && asyncCount == 0)
            {
                throw new InvalidOperationException(
                    $"Query context '{queryContextType.Name}' does not have a registered source. " +
                    $"Register exactly one IQueryContextSource<{queryContextType.Name}> or IQueryContextSourceAsync<{queryContextType.Name}>.");
            }

            if (syncCount > 0 && asyncCount > 0)
            {
                throw new InvalidOperationException(
                    $"Query context '{queryContextType.Name}' has both a sync and async source registered. " +
                    $"Register exactly one: IQueryContextSource<{queryContextType.Name}> or IQueryContextSourceAsync<{queryContextType.Name}>.");
            }

            if (syncCount > 1 || asyncCount > 1)
            {
                throw new InvalidOperationException(
                    $"Query context '{queryContextType.Name}' has multiple registered local sources.");
            }
        }
    }
}