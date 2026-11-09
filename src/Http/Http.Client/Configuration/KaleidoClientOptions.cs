namespace Kaleido.Http.Client.Configuration;

/// <summary>
/// Gateway HTTP client settings, bound from the <c>Kaleido</c> section by <c>AddHttpClients</c>.
/// </summary>
public sealed record KaleidoClientOptions
{
    /// <summary>
    /// The one gateway base URL used for every Process, Queryable and control-registry call.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Resilience of the gateway client (<c>Kaleido:Resilience</c>): timeouts, retries of safe requests
    /// and a circuit breaker, on by default. See <see cref="KaleidoResilienceOptions"/>.
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

    // Invalid shared resilience settings stop startup.
    internal static void Validate(KaleidoClientOptions options)
    {
        KaleidoResilienceOptions.Validate(options.Resilience, $"{KaleidoServiceOptions.SectionName}:{nameof(Resilience)}");
    }
}
