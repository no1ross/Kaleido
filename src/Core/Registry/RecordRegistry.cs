using Kaleido.Metadata;

namespace Kaleido.Registry;

public sealed class RecordRegistry : IRecordRegistry
{
    private readonly IReadOnlyDictionary<string, RecordRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, RecordRegistration> _byType;
    private readonly IReadOnlyCollection<RecordRegistration> _registrations;

    public RecordRegistry(
        IEnumerable<RecordRegistration> registrations)
    {
        var items = registrations.ToArray();

        _registrations = items;

        _byName = items.ToDictionary(
            x => x.Name,
            StringComparer.OrdinalIgnoreCase);

        _byType = items.ToDictionary(
            x => x.RecordType);
    }

    public IReadOnlyCollection<RecordRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<RecordRegistration> GetAll() =>
        _registrations;

    public RecordRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public RecordRegistration? Find(Type recordType)
    {
        _byType.TryGetValue(
            recordType,
            out var registration);

        return registration;
    }

    public RecordRegistration GetRequired(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Record '{name}' is not registered.");
    }

    public RecordRegistration GetRequired(Type recordType)
    {
        return Find(recordType)
            ?? throw new KeyNotFoundException(
                $"Record type '{recordType.FullName}' is not registered.");
    }
}