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
public sealed record KaleidoEventOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "Events";

    /// <summary>The most entries one claim takes. Default 20.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>The most publishes running at once on this instance. Default 1 (one after another).</summary>
    public int MaxConcurrentPublishes { get; init; } = 1;

    /// <summary>
    /// Timeout, retries and circuit breaker for publishing. Defaults: 30 s per attempt, 9 retries
    /// (10 attempts) from 1 s, circuit breaker 0.5 / 5 / 30 s / 30 s.
    /// </summary>
    public KaleidoResilienceSettings Resilience { get; init; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(30),
        MaxRetryAttempts = 9,
        RetryDelay = TimeSpan.FromSeconds(1),
        CircuitBreakerMinimumThroughput = 5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
    };

    internal static void Validate(KaleidoEventOptions? options)
    {
        if (options is null)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidEventOptions, "Kaleido:Events must not be null.");
        }

        Require(options.BatchSize > 0, nameof(BatchSize), "must be positive");
        Require(options.MaxConcurrentPublishes > 0, nameof(MaxConcurrentPublishes), "must be positive");
        KaleidoResilienceSettings.Validate(options.Resilience, "Kaleido:Events:Resilience", ConfigurationErrorCodes.InvalidEventOptions);
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidEventOptions, $"Kaleido:Events:{name} {rule}.");
        }
    }
}
