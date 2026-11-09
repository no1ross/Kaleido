using Kaleido.Exceptions;

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
/// <strong>Only safe requests are retried:</strong> reads (GET, HEAD) and requests that carry an
/// <c>Idempotency-Key</c> - the Kaleido process client sends one with every execute call, and the service
/// answers a retry with the stored result instead of running the step again. A request without either is
/// never retried. Retries re-send the same request, so the correlation headers (the same request id) and
/// the idempotency key stay the same.
/// </para>
/// <para>
/// The timeout, retry and circuit-breaker settings come from <see cref="KaleidoResilienceSettings"/>
/// (defaults: 10 s per attempt, 3 retries of transient failures - 5xx, 408, 429, timeouts, connection
/// errors - from 500 ms with jitter, circuit breaker 0.5 / 10 / 30 s / 15 s).
/// </para>
/// </remarks>
public sealed record KaleidoResilienceOptions : KaleidoResilienceSettings
{
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
