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
public record KaleidoResilienceSettings
{
    /// <summary>The time for one attempt. Default 10 seconds.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often a failed attempt is retried. Default 3; 0 = no retries.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>The base delay before the first retry; it grows exponentially. Default 500 ms.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The share of failed attempts (within <see cref="CircuitBreakerSamplingDuration"/>) that opens the circuit. Default 0.5.</summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>The number of attempts within the sampling duration before the circuit can open. Default 10.</summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    /// <summary>The window the failure ratio is measured over. Default 30 seconds.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long an open circuit stops attempts before it tries again. Default 15 seconds.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    // The rules every resilience section shares (Kaleido:Events:Resilience, Kaleido:Resilience,
    // Kaleido:Clients:{name}:Resilience), reported with the section's path and the area's code.
    internal static void Validate(KaleidoResilienceSettings? settings, string section, string errorCode)
    {
        if (settings is null)
        {
            throw new KaleidoConfigurationException(errorCode, $"{section} must not be null.");
        }

        Require(settings.AttemptTimeout > TimeSpan.Zero, "AttemptTimeout", "must be positive");
        Require(settings.MaxRetryAttempts >= 0, "MaxRetryAttempts", "must not be negative");
        Require(settings.RetryDelay > TimeSpan.Zero, "RetryDelay", "must be positive");
        Require(settings.CircuitBreakerFailureRatio is > 0 and <= 1, "CircuitBreakerFailureRatio", "must be greater than 0 and at most 1");
        Require(settings.CircuitBreakerMinimumThroughput >= 2, "CircuitBreakerMinimumThroughput", "must be at least 2");
        Require(settings.CircuitBreakerSamplingDuration >= TimeSpan.FromMilliseconds(500), "CircuitBreakerSamplingDuration", "must be at least 500 ms");
        Require(settings.CircuitBreakerBreakDuration >= TimeSpan.FromMilliseconds(500), "CircuitBreakerBreakDuration", "must be at least 500 ms");

        void Require(bool condition, string name, string rule)
        {
            if (!condition)
            {
                throw new KaleidoConfigurationException(errorCode, $"{section}:{name} {rule}.");
            }
        }
    }
}
