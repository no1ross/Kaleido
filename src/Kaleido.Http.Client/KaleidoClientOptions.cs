using System.Globalization;
using Kaleido.Exceptions;

namespace Kaleido.Http.Client;

/// <summary>
/// Configuration options for registering downstream Kaleido clients.
/// Bound from the <c>Kaleido</c> configuration section by
/// <c>AddHttpClients</c>.
/// </summary>
public sealed record KaleidoClientOptions
{
    /// <summary>
    /// Default base URL applied to every client entry whose own <c>BaseUrl</c> is unset.
    /// Typically the Kaleido router address (e.g. <c>"http://router:8080"</c>).
    /// Individual entries can override this by supplying their own <c>BaseUrl</c>.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// How often a client checks whether the remote registry changed - the shared default for
    /// <see cref="KaleidoClientEntry.RegistryTtl"/>. Default 30 seconds.
    /// </summary>
    /// <remarks>
    /// A registry changes only with a deployment, but nothing tells a router (or an MCP adapter) that a
    /// downstream was deployed; the check is how it finds out. A check sends the known revision as an
    /// ETag and an unchanged registry answers an empty 304, so a short interval is cheap. Null means
    /// the registry is never checked again until it is invalidated (a 404 from the service, or
    /// <c>InvalidateRegistryAsync()</c>).
    /// </remarks>
    public TimeSpan? RegistryTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Resilience shared by all clients (<c>Kaleido:Resilience</c>): timeouts, retries of safe requests and
    /// a circuit breaker, on by default. A client overrides single settings in
    /// <c>Kaleido:Clients:{name}:Resilience</c>; see <see cref="KaleidoResilienceOptions"/>.
    /// </summary>
    public KaleidoResilienceOptions Resilience { get; init; } = new();

    /// <summary>
    /// Shared default for <see cref="KaleidoClientEntry.StrictRegistryProbe"/>.
    /// </summary>
    public bool StrictRegistryProbe { get; init; }

    /// <summary>
    /// Named downstream Kaleido service clients.
    /// The dictionary key is the client name (e.g. <c>"Member"</c>).
    /// <see cref="KaleidoClientEntry.RoutePrefix"/> defaults to the key lowercased
    /// when not explicitly set.
    /// <see cref="KaleidoClientEntry.BaseUrl"/> falls back to <see cref="BaseUrl"/>
    /// when not explicitly set on the entry.
    /// </summary>
    /// <remarks>
    /// Bound from configuration (<c>Kaleido:Clients</c>); the configuration binder populates
    /// read-only dictionary properties, so the options stay read-only to the code that consumes them.
    /// </remarks>
    public IReadOnlyDictionary<string, KaleidoClientEntry> Clients { get; init; } =
        new Dictionary<string, KaleidoClientEntry>(StringComparer.OrdinalIgnoreCase);

    // Run by AddHttpClients after configuration is laid over the defaults: the shared settings and each
    // client's own; nothing is adjusted, an invalid value stops startup. (Each client's resilience is
    // validated once its own section is laid over the shared one.)
    internal static void Validate(KaleidoClientOptions options)
    {
        RequireZeroOrMore(options.RegistryTtl, $"{KaleidoServiceOptions.SectionName}:{nameof(RegistryTtl)}");
        KaleidoResilienceOptions.Validate(options.Resilience, $"{KaleidoServiceOptions.SectionName}:{nameof(Resilience)}");

        foreach (var (name, entry) in options.Clients)
        {
            RequireZeroOrMore(entry.RegistryTtl, $"{KaleidoServiceOptions.SectionName}:{nameof(Clients)}:{name}:{nameof(KaleidoClientEntry.RegistryTtl)}");
        }
    }

    private static void RequireZeroOrMore(TimeSpan? value, string path)
    {
        if (value < TimeSpan.Zero)
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                string.Create(CultureInfo.InvariantCulture, $"{path} is {value}; it must be zero or more when set (zero checks the registry on every use)."));
        }
    }
}

/// <summary>
/// Configuration for a single named Kaleido downstream client.
/// </summary>
public sealed record KaleidoClientEntry
{
    /// <summary>
    /// The base URL of the remote Kaleido service (e.g. <c>"http://router:8080"</c>).
    /// When null or empty, falls back to <see cref="KaleidoClientOptions.BaseUrl"/>.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// The route prefix used by the remote service (e.g. <c>"member"</c>).
    /// When null or unset, defaults to the dictionary key lowercased.
    /// </summary>
    public string? RoutePrefix { get; init; }

    /// <summary>
    /// How often this client checks whether the remote registry changed (ETag check, an unchanged
    /// registry answers 304). Overrides <see cref="KaleidoClientOptions.RegistryTtl"/>; null (unset)
    /// uses the shared value.
    /// </summary>
    public TimeSpan? RegistryTtl { get; init; }

    /// <summary>
    /// Overrides <see cref="KaleidoClientOptions.StrictRegistryProbe"/> —
    /// when true the health check probes <c>?strict</c> so a partially
    /// degraded aggregating downstream reports Unhealthy.
    /// </summary>
    public bool? StrictRegistryProbe { get; init; }
}
