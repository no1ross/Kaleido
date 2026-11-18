namespace Kaleido.Http.Client.Resilience;

/// <summary>
/// Resilience for a Kaleido client's outbound calls: a total timeout, retries with exponential backoff
/// and jitter, a circuit breaker and a per-attempt timeout (built on <c>Microsoft.Extensions.Http.Resilience</c>).
/// On by default.
/// </summary>
/// <remarks>
/// <para>
/// Framework defaults are overridden by the shared <c>Kaleido:Resilience</c> section for the gateway
/// and registry clients.
/// </para>
/// <para>
/// The shared pipeline retries safe reads (GET, HEAD) only. The Process HTTP client's own named
/// pipeline additionally retries transient POST failures only with one stable <c>Idempotency-Key</c>;
/// unrelated POSTs are never made retryable. Its retries preserve the same request, correlation
/// headers and key so the called service can replay its stored answer.
/// </para>
/// <para>
/// The timeout, retry and circuit-breaker settings come from <see cref="KaleidoResilienceSettings"/>
/// (defaults: 10 s per attempt, 3 retries of transient failures - 5xx, 408, 429, timeouts, connection
/// errors - from 500 ms with jitter, circuit breaker 0.5 / 10 / 30 s / 15 s).
/// </para>
/// </remarks>
public sealed record KaleidoResilienceOptions : KaleidoResilienceSettings, IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido:Resilience";

    void IKaleidoOptions.Validate() => Validate(this, ((IKaleidoOptions)this).ConfigurationPath);

    /// <summary>False turns resilience off (for the client, or shared for all clients). Default true.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The time for a whole call, all attempts and delays included. Default 30 seconds.</summary>
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(30);

    // A request with this option set is never retried.
    internal static readonly HttpRequestOptionsKey<bool> NoRetry = new("Kaleido.NoRetry");

    // The shared rules and timeouts; an invalid value stops startup.
    internal static void Validate(KaleidoResilienceOptions resilience, string section)
    {
        Validate(resilience, section, HttpClientErrorCodes.InvalidClientOptions);

        if (resilience.TotalTimeout <= TimeSpan.Zero)
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                $"{section}:TotalTimeout ({resilience.TotalTimeout}) must be positive.");
        }
    }
}
