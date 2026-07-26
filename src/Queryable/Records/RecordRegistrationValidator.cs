using Kaleido.Queryable.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

public interface IRecordRegistrationValidator
{
    void Validate(
        IReadOnlyCollection<Type> recordTypes,
        IServiceCollection services);
}

internal sealed class RecordRegistrationValidator
    : IRecordRegistrationValidator
{
    public void Validate(
        IReadOnlyCollection<Type> recordTypes,
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(recordTypes);
        ArgumentNullException.ThrowIfNull(services);

        ValidateDuplicateRecordNames(
            recordTypes);

        foreach (var recordType in recordTypes)
        {
            ValidateNamedQueries(
                recordType,
                services);
        }
    }

    private static void ValidateDuplicateRecordNames(
        IReadOnlyCollection<Type> recordTypes)
    {
        var duplicates =
            recordTypes
                .Select(x => new
                {
                    Type = x,
                    Attribute =
                        x.GetCustomAttribute<KaleidoRecordAttribute>()
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
            $"Duplicate record names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }

    private static void ValidateNamedQueries(
        Type recordType,
        IServiceCollection services)
    {
        var queryInterface =
            typeof(IRecordNamedQuery<>)
                .MakeGenericType(recordType);

        var duplicates =
            services
                .Where(x => x.ServiceType == queryInterface)
                .Select(x => x.ImplementationType)
                .Where(x => x is not null)
                .Select(x => new
                {
                    Type = x!,
                    Attribute =
                        x!.GetCustomAttribute<NamedQueryAttribute>()
                })
                .Where(x => x.Attribute is not null)
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
            $"Record '{recordType.Name}' contains duplicate named queries: " +
            $"{string.Join(", ", duplicates.Select(x => x.Key))}");
    }
}