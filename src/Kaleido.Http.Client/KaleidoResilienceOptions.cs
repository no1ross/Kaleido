using Kaleido.Exceptions;
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

    /// <summary>The timeout of the client health check (which is never retried). Default 5 seconds.</summary>
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(5);

    // A request with this option set is never retried (the client health check).
    internal static readonly HttpRequestOptionsKey<bool> NoRetry = new("Kaleido.NoRetry");

    // These (shared) settings with the ones the client's own Resilience section names laid over a copy
    // (ADR 0030), validated.
    internal KaleidoResilienceOptions ForClient(IConfiguration configuration, string clientName)
    {
        var section = $"{KaleidoServiceOptions.SectionName}:Clients:{clientName}:Resilience";
        var resilience = configuration.GetSection(section).BindOver(this, HttpClientErrorCodes.InvalidClientOptions);
        Validate(resilience, section);

        return resilience;
    }

    // The shared rules, then the client-only timeouts; nothing is adjusted, an invalid value stops startup.
    internal static void Validate(KaleidoResilienceOptions resilience, string section)
    {
        Validate(resilience, section, HttpClientErrorCodes.InvalidClientOptions);

        if (resilience.TotalTimeout <= TimeSpan.Zero || resilience.HealthCheckTimeout <= TimeSpan.Zero)
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                $"{section}:TotalTimeout ({resilience.TotalTimeout}) and {section}:HealthCheckTimeout ({resilience.HealthCheckTimeout}) must be positive.");
        }
    }
}
