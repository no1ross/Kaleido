namespace Kaleido.Queryable.Registry;

public static class RegistrationValidator
{
    public static void Validate(
        RecordDiscoveryResult discovery)
    {
        ValidateDuplicateRecordNames(discovery);
        ValidateDuplicateRecordKeys(discovery);
        ValidateDuplicateSources(discovery);
        ValidateMissingSources(discovery);
    }

    private static void ValidateDuplicateSources(
        RecordDiscoveryResult discovery)
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
        RecordDiscoveryResult discovery)
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
        RecordDiscoveryResult discovery)
    {
        var duplicates = discovery.Records
            .GroupBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Duplicate record names detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }

    private static void ValidateDuplicateRecordKeys(
        RecordDiscoveryResult discovery)
    {
        var duplicates = discovery.Records
            .GroupBy(x => x.Metadata.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Duplicate record keys detected: {string.Join(", ", duplicates.Select(x => x.Key))}");
    }
}