using Microsoft.Extensions.Configuration;

namespace Kaleido.Http.Client;

/// <summary>
/// Resilience for a Kaleido client's outbound calls: a total timeout, retries with exponential backoff
/// and jitter, a circuit breaker and a per-attempt timeout (built on <c>Microsoft.Extensions.Http.Resilience</c>).
/// On by default.
/// </summary>
/// <remarks>
/// <para>
/// Three layers: the framework defaults below, the shared <c>Kaleido:Resilience</c> section for all
/// clients, and <c>Kaleido:Clients:{name}:Resilience</c> per client - a client's section overrides only
/// the settings it names, the rest come from the shared section.
/// </para>
/// <para>
/// <strong>Only safe requests are retried:</strong> reads (GET, HEAD) and requests that carry an
/// <c>Idempotency-Key</c> - the Kaleido process client sends one with every execute call, and the service
/// answers a retry with the stored result instead of running the step again. A request without either is
/// never retried. Retries re-send the same request, so the correlation headers (the same request id) and
/// the idempotency key stay the same. The client health check is never retried and has its own short
/// timeout (<see cref="HealthCheckTimeout"/>).
/// </para>
/// </remarks>
public sealed class KaleidoResilienceOptions
{
    /// <summary>False turns resilience off (for the client, or shared for all clients). Default true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The time for a whole call, all attempts and delays included. Default 30 seconds.</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The time for one attempt. Default 10 seconds.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often a failed safe request is retried (transient failures: 5xx, 408, 429, timeouts, connection
    /// errors). Default 3; 0 = no retries.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>The base delay before the first retry; it grows exponentially, with jitter. Default 500 ms.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The share of failed calls (within <see cref="CircuitBreakerSamplingDuration"/>) that opens the circuit. Default 0.5.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>The number of calls within the sampling duration before the circuit can open. Default 10.</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>The window the failure ratio is measured over. Default 30 seconds.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long an open circuit fails calls immediately before it tries again. Default 15 seconds.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>The timeout of the client health check (which is never retried). Default 5 seconds.</summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // A request with this option set is never retried (the client health check).
    internal static readonly HttpRequestOptionsKey<bool> NoRetry = new("Kaleido.NoRetry");

    // These (shared) settings, overridden by the ones the client's own Resilience section names.
    internal KaleidoResilienceOptions ForClient(IConfiguration configuration, string clientName)
    {
        var resilience = (KaleidoResilienceOptions)MemberwiseClone();
        configuration.GetSection($"{KaleidoServiceOptions.SectionName}:Clients:{clientName}:Resilience").Bind(resilience);
        return resilience;
    }
}
