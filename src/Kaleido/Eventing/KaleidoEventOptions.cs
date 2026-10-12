namespace Kaleido.Eventing;

/// <summary>
/// How the event relay hands outbox entries to <see cref="IEventPublisher"/>. Bound from
/// <c>Kaleido:Events</c>; every value has a default, so the section is optional.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Load per instance:</strong> one claim takes at most <see cref="BatchSize"/> entries, the
/// next claim happens only when the batch is done, and at most <see cref="MaxConcurrentPublishes"/>
/// publishes run at once. With several instances on one store the backlog spreads over them in
/// batches; an entry is never claimed by two at once.
/// </para>
/// <para>
/// <strong><see cref="Resilience"/> - two layers:</strong>
/// <see cref="KaleidoResilienceSettings.MaxRetryAttempts"/> and
/// <see cref="KaleidoResilienceSettings.RetryDelay"/> are the <em>durable</em> schedule kept in the
/// outbox (the delay doubles per attempt, capped at five minutes; after the last retry the entry is
/// parked - kept, logged at Error, never dropped); it survives restarts and is shared by every
/// instance. <see cref="KaleidoResilienceSettings.AttemptTimeout"/> and the circuit breaker apply to
/// each attempt: while the circuit is open the relay claims nothing, so a publisher outage neither
/// burns attempts nor floods the logs.
/// </para>
/// <code>
/// "Kaleido": {
///   "Events": {
///     "BatchSize": 20,
///     "MaxConcurrentPublishes": 1,
///     "Resilience": { "AttemptTimeout": "00:00:30", "MaxRetryAttempts": 9 }
///   }
/// }
/// </code>
/// </remarks>
public sealed class KaleidoEventOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "Events";

    /// <summary>The most entries one claim takes. Default 20.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>The most publishes running at once on this instance. Default 1 (one after another).</summary>
    public int MaxConcurrentPublishes { get; set; } = 1;

    /// <summary>
    /// Timeout, retries and circuit breaker for publishing. Defaults: 30 s per attempt, 9 retries
    /// (10 attempts) from 1 s, circuit breaker 0.5 / 5 / 30 s / 30 s.
    /// </summary>
    public KaleidoResilienceSettings Resilience { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(30),
        MaxRetryAttempts = 9,
        RetryDelay = TimeSpan.FromSeconds(1),
        CircuitBreakerMinimumThroughput = 5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
    };

    // A copy, so the registered options cannot be changed after AddKaleido.
    internal KaleidoEventOptions Copy() =>
        new()
        {
            BatchSize = BatchSize,
            MaxConcurrentPublishes = MaxConcurrentPublishes,
            Resilience = new KaleidoResilienceSettings
            {
                AttemptTimeout = Resilience.AttemptTimeout,
                MaxRetryAttempts = Resilience.MaxRetryAttempts,
                RetryDelay = Resilience.RetryDelay,
                CircuitBreakerFailureRatio = Resilience.CircuitBreakerFailureRatio,
                CircuitBreakerMinimumThroughput = Resilience.CircuitBreakerMinimumThroughput,
                CircuitBreakerSamplingDuration = Resilience.CircuitBreakerSamplingDuration,
                CircuitBreakerBreakDuration = Resilience.CircuitBreakerBreakDuration,
            },
        };

    internal static void Validate(KaleidoEventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resilience = options.Resilience
            ?? throw Invalid(nameof(Resilience), "must not be null");

        Require(options.BatchSize > 0, nameof(BatchSize), "must be positive");
        Require(options.MaxConcurrentPublishes > 0, nameof(MaxConcurrentPublishes), "must be positive");
        Require(resilience.AttemptTimeout > TimeSpan.Zero, "Resilience:AttemptTimeout", "must be positive");
        Require(resilience.MaxRetryAttempts >= 0, "Resilience:MaxRetryAttempts", "must not be negative");
        Require(resilience.RetryDelay > TimeSpan.Zero, "Resilience:RetryDelay", "must be positive");
        Require(resilience.CircuitBreakerFailureRatio is > 0 and <= 1, "Resilience:CircuitBreakerFailureRatio", "must be greater than 0 and at most 1");
        Require(resilience.CircuitBreakerMinimumThroughput >= 2, "Resilience:CircuitBreakerMinimumThroughput", "must be at least 2");
        Require(resilience.CircuitBreakerSamplingDuration >= TimeSpan.FromMilliseconds(500), "Resilience:CircuitBreakerSamplingDuration", "must be at least 500 ms");
        Require(resilience.CircuitBreakerBreakDuration >= TimeSpan.FromMilliseconds(500), "Resilience:CircuitBreakerBreakDuration", "must be at least 500 ms");
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw Invalid(name, rule);
        }
    }

    private static KaleidoConfigurationException Invalid(string name, string rule) =>
        new(ConfigurationErrorCodes.InvalidEventOptions, $"Kaleido:Events:{name} {rule}.");
}
