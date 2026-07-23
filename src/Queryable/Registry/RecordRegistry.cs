using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Registry;

public sealed class RecordRegistry : IRecordRegistry
{
    private readonly IReadOnlyDictionary<string, RecordRegistration> _byName;
    private readonly IReadOnlyDictionary<string, RecordRegistration> _byKey;
    private readonly IReadOnlyDictionary<Type, RecordRegistration> _byType;
    private readonly IReadOnlyCollection<RecordRegistration> _registrations;

    public RecordRegistry(
        IEnumerable<RecordRegistration> registrations)
    {
        var items = registrations.ToArray();

        _registrations = items;

        _byName = items.ToDictionary(
            x => x.RuntimeMetadata.Name,
            StringComparer.OrdinalIgnoreCase);

        _byKey = items.ToDictionary(
            x => x.RuntimeMetadata.Key,
            StringComparer.OrdinalIgnoreCase);

        _byType = items.ToDictionary(
            x => x.RecordType);
    }

    public IReadOnlyCollection<RecordRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<RecordRegistration> GetAll() =>
        _registrations;

    public RecordRegistration? FindByName(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public RecordRegistration? FindByKey(string key)
    {
        _byKey.TryGetValue(
            key,
            out var registration);

        return registration;
    }

    public RecordRegistration? FindByType(Type recordType)
    {
        _byType.TryGetValue(
            recordType,
            out var registration);

        return registration;
    }

    public RecordRegistration GetRegistration(string name)
    {
        return FindByName(name)
            ?? throw new KeyNotFoundException(
                $"Record '{name}' is not registered.");
    }

    public RecordRegistration GetRegistration(Type recordType)
    {
        return FindByType(recordType)
            ?? throw new KeyNotFoundException(
                $"Record type '{recordType.FullName}' is not registered.");
    }
}