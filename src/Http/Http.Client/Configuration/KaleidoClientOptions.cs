namespace Kaleido.Http.Client.Configuration;

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
    /// Resilience shared by all clients (<c>Kaleido:Resilience</c>): timeouts, retries of safe requests and
    /// a circuit breaker, on by default. A client overrides single settings in
    /// <c>Kaleido:Clients:{name}:Resilience</c>; see <see cref="KaleidoResilienceOptions"/>.
    /// </summary>
    public KaleidoResilienceOptions Resilience { get; init; } = new();

    /// <summary>
    /// How this service makes the calls it makes for a caller (<c>Kaleido:OnBehalfOfCalls</c>, #574): a
    /// setting read once at startup, not a token. <see cref="KaleidoOnBehalfOfCalls.Forward"/> (the default)
    /// sends the token the caller's request arrived with, unchanged; <see cref="KaleidoOnBehalfOfCalls.Exchange"/>
    /// sends the token the host's own handler gets for the caller at the time of the call
    /// (<c>AddKaleidoClientHandler&lt;THandler&gt;()</c>), never the inbound one. An MCP host requires
    /// <see cref="KaleidoOnBehalfOfCalls.Exchange"/>.
    /// </summary>
    public KaleidoOnBehalfOfCalls OnBehalfOfCalls { get; init; }

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
        KaleidoResilienceOptions.Validate(options.Resilience, $"{KaleidoServiceOptions.SectionName}:{nameof(Resilience)}");
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

}
