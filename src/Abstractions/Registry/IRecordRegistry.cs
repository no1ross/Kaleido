using Kaleido.Metadata;

namespace Kaleido.Registry;

/// <summary>
/// Maintains the list of registered records available
/// to the application.
///
/// While IRecordMetadataCatalog is responsible for generating
/// metadata for a specific record type, the registry is responsible
/// for discovering which records exist within the application.
///
/// Think of this component as the directory of available records.
/// </summary>
///
/// <remarks>
/// MetadataCatalog = describes one record
/// Registry = knows all records
/// </remarks>
public interface IRecordRegistry
{
    IReadOnlyCollection<RecordRegistration> Registrations { get; }

    IReadOnlyCollection<RecordRegistration> GetAll();

    RecordRegistration? Find(string name);

    RecordRegistration? Find(Type recordType);

    RecordRegistration GetRequired(string name);

    RecordRegistration GetRequired(Type recordType);
}