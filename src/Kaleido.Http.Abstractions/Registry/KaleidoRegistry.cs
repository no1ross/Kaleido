using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

/// <summary>
/// The registry: every processor and query source a host knows, in one shape everywhere (#553). The
/// control registry serves it (<c>GET /registry</c>), each participant keeps the copy it read as its own
/// registry, a client keeps the copy of the service it calls, and a host serving its registry to a caller
/// (<c>GET /{service}/registry</c>) filters it for that caller. Each service version's status is a
/// separate document, <see cref="KaleidoRegistryStatus"/>.
/// </summary>
/// <remarks>
/// Not sealed: the transport-neutral model is meant to be shared by every transport, each adding how its
/// capabilities are reached (#557, #560).
/// </remarks>
[ExcludeFromCodeCoverage]
public record KaleidoRegistry
{
    /// <summary>
    /// The processors: on a host reading the control registry, every service version's; on a host with
    /// none, its own (and, with <c>AddHttpClients()</c>, its clients' services').
    /// </summary>
    public IReadOnlyCollection<ProcessorRegistryResponse> Processes { get; init; }
        = [];

    /// <summary>The query sources, from the same services as <see cref="Processes"/>.</summary>
    public IReadOnlyCollection<QueryableSourceResponse> Queryables { get; init; }
        = [];

    /// <summary>
    /// The parts of the registry that could not be read (a client's service, or the control registry). A
    /// non-empty collection makes the registry partial: <see cref="Processes"/> and
    /// <see cref="Queryables"/> hold only what was read.
    /// </summary>
    public IReadOnlyCollection<RegistryReadError> Errors { get; init; }
        = [];

    /// <summary>
    /// What the control registry expects and does not have (#553): a configured processor version, or a
    /// service a registered step looks up, with no registration. A non-empty collection makes the
    /// registry partial.
    /// </summary>
    public IReadOnlyCollection<RegistryMissingService> Missing { get; init; }
        = [];

    /// <summary>
    /// Convenience flag — <see langword="true"/> when <see cref="Errors"/> or
    /// <see cref="Missing"/> is non-empty: part of the registry could not be read, or an expected service
    /// is absent. Lets agents detect degradation without parsing the lists.
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>
    /// UTC timestamp of the snapshot build. When the registry is served from a cache (server or distributed
    /// snapshot store), this reflects the age of the underlying data — not the time it was sent.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Content hash of the registry (SHA-256, lowercase hex). Mirrors the <c>ETag</c> response header —
    /// send it back as <c>If-None-Match</c> to receive a 304 when nothing changed.
    /// </summary>
    public string? Revision { get; init; }
}

/// <summary>A part of the registry that could not be read.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryReadError
{
    /// <summary>
    /// What could not be read: a client's name under <c>Kaleido:Clients</c> (e.g. <c>"Member"</c>), or
    /// <c>"registry"</c> for the control registry.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>A human-readable description of the failure.</summary>
    public required string Reason { get; init; }
}
