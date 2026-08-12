using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

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
public interface IQueryContextRegistry
{
    IReadOnlyCollection<QueryContextRegistration> Registrations { get; }

    QueryContextRegistration? Find(string name);

    QueryContextRegistration? Find(Type recordType);

    QueryContextRegistration GetRegistration(string name);

    QueryContextRegistration GetRegistration(Type recordType);
}