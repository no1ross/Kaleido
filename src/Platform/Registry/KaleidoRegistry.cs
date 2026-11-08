using Kaleido.Platform.Processor;
using Kaleido.Platform.Queryable;

namespace Kaleido.Platform.Registry;

/// <summary>
/// The central registry's view of every processor and query source (#553). The registry serves a
/// caller-filtered view at <c>GET /registry</c>; participants hydrate a full copy and filter it for
/// in-host consumers such as MCP. Each service version's status is a separate document,
/// <see cref="KaleidoRegistryStatus"/>.
/// </summary>
/// <remarks>
/// Not sealed: the transport-neutral model is meant to be shared by every transport, each adding how its
/// capabilities are reached (#557, #560).
/// </remarks>
[ExcludeFromCodeCoverage]
public record KaleidoRegistry
{
    /// <summary>
    /// The processors published by the central registry, across every registered service version.
    /// </summary>
    public IReadOnlyCollection<ProcessorRegistryResponse> Processes { get; init; }
        = [];

    /// <summary>The query sources, from the same services as <see cref="Processes"/>.</summary>
    public IReadOnlyCollection<QueryableSourceResponse> Queryables { get; init; }
        = [];

    /// <summary>
    /// Registry reads that failed while a participant keeps its last good central copy. A non-empty
    /// collection makes the registry partial: <see cref="Processes"/> and
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
    /// UTC timestamp of the central registry's view. When served from a cache, it reflects the age
    /// of the underlying data — not the time it was sent.
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
    /// What could not be read: <c>"registry"</c> when a participant cannot refresh its central copy.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>A human-readable description of the failure.</summary>
    public required string Reason { get; init; }
}
