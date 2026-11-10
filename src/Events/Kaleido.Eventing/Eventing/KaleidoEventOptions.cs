using Microsoft.Extensions.Diagnostics.HealthChecks;

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
///     "Resilience": { "AttemptTimeout": "00:00:30", "MaxRetryAttempts": 9 },
///     "RelaySeenWithin": "00:02:00", "BacklogMaxAge": "00:05:00"
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

    /// <summary>
    /// How often a relay records its heartbeat in the outbox it drains (#552). Default 30 seconds.
    /// </summary>
    public TimeSpan RelayHeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The outbox health check (every service) fails when no relay was seen within this window: nothing
    /// publishes the events, and the outbox keeps growing. Default 2 minutes; longer than
    /// <see cref="RelayHeartbeatInterval"/>.
    /// </summary>
    public TimeSpan RelaySeenWithin { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The outbox health check fails when the oldest due entry is older than this: a relay runs but does
    /// not keep up, or its publisher keeps failing. Default 5 minutes.
    /// </summary>
    public TimeSpan BacklogMaxAge { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The outbox health check fails when more entries than this are due. Default 10,000.</summary>
    public int BacklogMaxCount { get; init; } = 10_000;

    /// <summary>
    /// What the outbox health check reports when it fails. Default <see cref="HealthStatus.Degraded"/>: the
    /// service still works and its events wait, so it is a signal for operators rather than a reason to stop
    /// sending it traffic. Set <see cref="HealthStatus.Unhealthy"/> to fail readiness on it.
    /// </summary>
    public HealthStatus OutboxHealthFailureStatus { get; init; } = HealthStatus.Degraded;

    internal static void Validate(KaleidoEventOptions? options)
    {
        if (options is null)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidEventOptions, "Kaleido:Events must not be null.");
        }

        Require(options.BatchSize > 0, nameof(BatchSize), "must be positive");
        Require(options.MaxConcurrentPublishes > 0, nameof(MaxConcurrentPublishes), "must be positive");
        Require(options.RelayHeartbeatInterval > TimeSpan.Zero, nameof(RelayHeartbeatInterval), "must be positive");
        Require(options.RelaySeenWithin > options.RelayHeartbeatInterval, nameof(RelaySeenWithin), "must be longer than RelayHeartbeatInterval");
        Require(options.BacklogMaxAge > TimeSpan.Zero, nameof(BacklogMaxAge), "must be positive");
        Require(options.BacklogMaxCount > 0, nameof(BacklogMaxCount), "must be positive");
        Require(Enum.IsDefined(options.OutboxHealthFailureStatus) && options.OutboxHealthFailureStatus != HealthStatus.Healthy, nameof(OutboxHealthFailureStatus), "must be Degraded or Unhealthy");
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
