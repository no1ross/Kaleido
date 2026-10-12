namespace Kaleido;

/// <summary>
/// The resilience settings Kaleido uses wherever it calls something that can fail transiently: a
/// timeout per attempt, retries with exponential backoff, and a circuit breaker. One vocabulary for
/// the HTTP clients (<c>Kaleido:Resilience</c>, <c>Kaleido:Clients:{name}:Resilience</c>) and the
/// event relay (<c>Kaleido:Events:Resilience</c>); each section has its own defaults and is bound
/// independently.
/// </summary>
/// <remarks>
/// The defaults below are the HTTP clients' defaults. The event relay starts from its own (see
/// <see cref="Eventing.KaleidoEventOptions"/>); a section overrides only the settings it names.
/// </remarks>
public class KaleidoResilienceSettings
{
    /// <summary>The time for one attempt. Default 10 seconds.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often a failed attempt is retried. Default 3; 0 = no retries.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>The base delay before the first retry; it grows exponentially. Default 500 ms.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The share of failed attempts (within <see cref="CircuitBreakerSamplingDuration"/>) that opens the circuit. Default 0.5.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>The number of attempts within the sampling duration before the circuit can open. Default 10.</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>The window the failure ratio is measured over. Default 30 seconds.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long an open circuit stops attempts before it tries again. Default 15 seconds.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
