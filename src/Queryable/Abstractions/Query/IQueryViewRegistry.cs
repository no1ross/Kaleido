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
public interface IQueryViewRegistry
{
    IReadOnlyCollection<QueryViewRegistration> Registrations { get; }

    QueryViewRegistration? Find(string name);

    QueryViewRegistration? Find(Type recordType);

    QueryViewRegistration GetRegistration(string name);

    QueryViewRegistration GetRegistration(Type recordType);
}