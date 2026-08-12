using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

public interface IQueryContextRegistrationValidator
{
    void Validate(
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services);
}

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
            var sourceInterface =
                typeof(IQueryContextSource<>)
                    .MakeGenericType(queryContextType);

            var registrations =
                services
                    .Where(x =>
                        x.ServiceType ==
                        sourceInterface)
                    .ToArray();

            if (registrations.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Query context '{queryContextType.Name}' does not have a registered source.");
            }

            if (registrations.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Query context '{queryContextType.Name}' has multiple registered sources.");
            }
        }
    }
}