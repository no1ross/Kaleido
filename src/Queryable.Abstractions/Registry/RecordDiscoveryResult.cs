using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Registry;

public sealed class RecordDiscoveryResult
{
    public required IReadOnlyList<RecordDiscovery> Records { get; init; }

    public required IReadOnlyList<SourceDiscovery> Sources { get; init; }

    public required IReadOnlyList<NamedQueryDiscovery> NamedQueries { get; init; }
}

public sealed record RecordDiscovery(
    Type RecordType,
    RuntimeRecordMetadata Metadata);

public sealed record SourceDiscovery(
    Type RecordType,
    Type InterfaceType,
    Type ImplementationType);

public sealed record NamedQueryDiscovery(
    Type RecordType,
    Type InterfaceType,
    Type ImplementationType);