namespace Kaleido.Http.Client;

/// <summary>
/// Configuration options for registering downstream Kaleido clients.
/// Bound from the <c>Kaleido</c> configuration section by
/// <c>AddHttpClients</c>.
/// </summary>
public sealed class KaleidoClientOptions
{
    /// <summary>
    /// Default base URL applied to every client entry whose own <c>BaseUrl</c> is unset.
    /// Typically the Kaleido router address (e.g. <c>"http://router:8080"</c>).
    /// Individual entries can override this by supplying their own <c>BaseUrl</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Shared default for <see cref="KaleidoClientEntry.RegistryTtl"/> applied
    /// to every client that does not set its own. Null (default) means the
    /// remote registry is cached until explicitly invalidated.
    /// </summary>
    public TimeSpan? RegistryTtl { get; set; }

    /// <summary>
    /// Shared default for <see cref="KaleidoClientEntry.StrictRegistryProbe"/>.
    /// </summary>
    public bool StrictRegistryProbe { get; set; }

    /// <summary>
    /// Named downstream Kaleido service clients.
    /// The dictionary key is the client name (e.g. <c>"Member"</c>).
    /// <see cref="KaleidoClientEntry.RoutePrefix"/> defaults to the key lowercased
    /// when not explicitly set.
    /// <see cref="KaleidoClientEntry.BaseUrl"/> falls back to <see cref="BaseUrl"/>
    /// when not explicitly set on the entry.
    /// </summary>
    // KAL0018: IDictionary is required here — the Options binder writes entries
    // into this dictionary during startup configuration binding; a read-only
    // interface would prevent binding entirely.
#pragma warning disable KAL0018
    public IDictionary<string, KaleidoClientEntry> Clients { get; set; } = new Dictionary<string, KaleidoClientEntry>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore KAL0018
}

/// <summary>
/// Configuration for a single named Kaleido downstream client.
/// </summary>
public sealed class KaleidoClientEntry
{
    /// <summary>
    /// The base URL of the remote Kaleido service (e.g. <c>"http://router:8080"</c>).
    /// When null or empty, falls back to <see cref="KaleidoClientOptions.BaseUrl"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The route prefix used by the remote service (e.g. <c>"member"</c>).
    /// When null or unset, defaults to the dictionary key lowercased.
    /// </summary>
    public string? RoutePrefix { get; set; }

    /// <summary>
    /// Maximum age of this client's cached registry snapshot. Overrides
    /// <see cref="KaleidoClientOptions.RegistryTtl"/>; null means the registry
    /// is cached until explicitly invalidated.
    /// </summary>
    public TimeSpan? RegistryTtl { get; set; }

    /// <summary>
    /// Overrides <see cref="KaleidoClientOptions.StrictRegistryProbe"/> —
    /// when true the health check probes <c>?strict</c> so a partially
    /// degraded aggregating downstream reports Unhealthy.
    /// </summary>
    public bool? StrictRegistryProbe { get; set; }
}
