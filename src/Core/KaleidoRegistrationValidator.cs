using Kaleido.Registry;

namespace Kaleido;

internal static class KaleidoRegistrationValidator
{
    public static void Validate(
        KaleidoDiscoveryResult discovery)
    {
        ValidateDuplicateRecordNames(discovery);
        ValidateDuplicateSources(discovery);
        ValidateMissingSources(discovery);
    }

    private static void ValidateDuplicateSources(
        KaleidoDiscoveryResult discovery)
    {
        var duplicates = discovery.Sources
            .GroupBy(x => x.RecordType)
            .Where(x => x.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            duplicates.Select(group =>
                $"{group.Key.Name}: {string.Join(", ", group.Select(x => x.ImplementationType.Name))}"));

        throw new InvalidOperationException(
            $"Multiple sources registered for a record type.{Environment.NewLine}{details}");
    }

    private static void ValidateMissingSources(
        KaleidoDiscoveryResult discovery)
    {
        var sourceTypes = discovery.Sources
            .Select(x => x.RecordType)
            .ToHashSet();

        var missing = discovery.Records
            .Where(x => !sourceTypes.Contains(x.RecordType))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The following records do not have a source: {string.Join(", ", missing.Select(x => x.RecordType.Name))}");
    }

    private static void ValidateDuplicateRecordNames(
        KaleidoDiscoveryResult discovery)
    {
        var duplicates = discovery.Records
            .GroupBy(x => x.Metadata.Name)
            .Where(x => x.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Duplicate record names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }
}