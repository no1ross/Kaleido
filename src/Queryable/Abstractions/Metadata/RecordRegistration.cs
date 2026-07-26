namespace Kaleido.Queryable.Metadata;

/// <summary>Associates a record key with a record type and runtime metadata.</summary>
public sealed record RecordRegistration(Type RecordType, Type SourceType, RecordMetadata Metadata, IReadOnlyCollection<NamedQueryRegistration> NamedQueryTypes);
