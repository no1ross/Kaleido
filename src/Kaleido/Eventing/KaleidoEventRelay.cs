using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Kaleido.Eventing;

// Hands outbox entries to the event publisher, one relay per instance. It claims a batch (the claim
// is the lock between instances), publishes each entry through a timeout and a circuit breaker, and
// removes it on success; a failure is retried later with backoff (the durable schedule lives in the
// outbox) and parked after the last retry.
internal sealed partial class KaleidoEventRelay : BackgroundService
{
    internal static readonly TimeSpan RelayInterval = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseMargin = TimeSpan.FromSeconds(30);

    private static readonly Meter Meter = new(KaleidoEventTelemetry.MeterName);
    private static readonly Counter<long> Published = Meter.CreateCounter<long>(KaleidoEventTelemetry.PublishedCounterName, description: "Events handed to the publisher.");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>(KaleidoEventTelemetry.FailedCounterName, description: "Failed publish attempts.");
    private static readonly Counter<long> Parked = Meter.CreateCounter<long>(KaleidoEventTelemetry.ParkedCounterName, description: "Events parked after the last retry.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _publisher;
    private readonly IKaleidoEventSignal _signal;
    private readonly ILogger<KaleidoEventRelay> _logger;
    private readonly KaleidoEventOptions _options;
    private readonly TimeProvider _time;
    private readonly CircuitBreakerStateProvider _circuit = new();
    private readonly ResiliencePipeline _pipeline;

    // When the open circuit's break ends (UTC ticks). Polly moves an open circuit to half-open only
    // when an execution is attempted after the break, so the relay must stop holding back then.
    private long _breakUntilTicks;

    public KaleidoEventRelay(
        IServiceScopeFactory scopeFactory,
        IEventPublisher publisher,
        IKaleidoEventSignal signal,
        KaleidoServiceOptions serviceOptions,
        TimeProvider time,
        ILogger<KaleidoEventRelay> logger)
    {
        _scopeFactory = scopeFactory;
        _time = time;
        _publisher = publisher;
        _signal = signal;
        _logger = logger;
        _options = serviceOptions.Events;

        // Long enough for a whole batch at the configured parallelism, plus a margin.
        var rounds = (_options.BatchSize + _options.MaxConcurrentPublishes - 1) / _options.MaxConcurrentPublishes;
        Lease = (_options.Resilience.AttemptTimeout * rounds) + LeaseMargin;

        _pipeline = BuildPipeline(_options.Resilience);
    }

    internal TimeSpan Lease { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Without a publisher nothing is ever written to the outbox.
        if (_publisher is INoOpEventPublisher)
        {
            return;
        }

        try
        {
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; claimed entries are released when their lease expires.
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var wait = RelayInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            // While the circuit is open nothing is claimed: the entries stay untouched in the outbox.
            if (CircuitIsOpen)
            {
                await Task.Delay(BreakRemaining, _time, stoppingToken);
                continue;
            }

            var claimed = await RelayBatchAsync(stoppingToken);

            // A full batch: more entries are probably due, claim again right away.
            if (claimed >= _options.BatchSize)
            {
                wait = RelayInterval;
                continue;
            }

            wait = claimed > 0 ? RelayInterval : Min(wait * 2, IdleInterval);
            await _signal.WaitAsync(wait, stoppingToken);
        }
    }

    // Claims one batch and publishes it; returns the number of entries claimed.
    internal async Task<int> RelayBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>();

            var leaseUntil = _time.GetUtcNow() + Lease;
            var entries = await store.ClaimEventsAsync(_options.BatchSize, Lease, cancellationToken);

            // The store of this scope is not thread-safe: its calls take turns.
            using var storeGate = new SemaphoreSlim(1, 1);

            await Parallel.ForEachAsync(
                entries,
                new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrentPublishes, CancellationToken = cancellationToken },
                (entry, token) => new ValueTask(RelayAsync(store, storeGate, entry, leaseUntil, token)));

            return entries.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRelayFailed(_logger, exception);
            return 0;
        }
    }

    private async Task RelayAsync(
        IKaleidoOutboxStore store,
        SemaphoreSlim storeGate,
        KaleidoOutboxEvent entry,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        // Not enough lease left for a full attempt, or the circuit opened meanwhile: leave the entry;
        // it is claimed again once its lease expires.
        if (CircuitIsOpen || _time.GetUtcNow() + _options.Resilience.AttemptTimeout > leaseUntil)
        {
            return;
        }

        try
        {
            await _pipeline.ExecuteAsync(
                static (state, token) => new ValueTask(state.Publisher.PublishOutboxEventAsync(state.Entry, token)),
                (Publisher: _publisher, Entry: entry),
                cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            // Not an attempt: the circuit refused it.
            return;
        }
        // Any failure of the publisher (foreign code: it may throw anything) or a timed-out attempt is a
        // failed attempt - except the host stopping, which is not the publisher's fault.
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            await WithGateAsync(storeGate, () => FailAsync(store, entry, exception));
            return;
        }

        await WithGateAsync(storeGate, () => store.CompleteEventAsync(entry.EventId, CancellationToken.None));
        Published.Add(1, Tag(entry));
        LogPublished(_logger, entry.EventType, entry.EventId);
    }

    private async Task FailAsync(IKaleidoOutboxStore store, KaleidoOutboxEvent entry, Exception exception)
    {
        var attempts = entry.Attempts + 1;
        Failed.Add(1, Tag(entry));

        if (attempts > _options.Resilience.MaxRetryAttempts)
        {
            await store.FailEventAsync(entry.EventId, retryAtUtc: null, exception.Message, CancellationToken.None);
            Parked.Add(1, Tag(entry));
            LogParked(_logger, exception, entry.EventType, entry.EventId, attempts);
            return;
        }

        var retryAt = _time.GetUtcNow() + RetryDelay(_options.Resilience.RetryDelay, attempts);
        await store.FailEventAsync(entry.EventId, retryAt, exception.Message, CancellationToken.None);
        LogRetryScheduled(_logger, exception, entry.EventType, entry.EventId, attempts, retryAt);
    }

    // The delay doubles per attempt, capped.
    internal static TimeSpan RetryDelay(TimeSpan baseDelay, int attempts) =>
        Min(baseDelay * Math.Pow(2, Math.Min(attempts - 1, 20)), MaxRetryDelay);

    // Open only until the break ends: the next attempt after it is the half-open probe that closes
    // the circuit again (or re-opens it). Nothing isolates the circuit (no manual control).
    private bool CircuitIsOpen =>
        _circuit.CircuitState is CircuitState.Open && BreakRemaining > TimeSpan.Zero;

    private TimeSpan BreakRemaining
    {
        get
        {
            var remaining = new DateTimeOffset(Interlocked.Read(ref _breakUntilTicks), TimeSpan.Zero) - _time.GetUtcNow();

            return remaining > TimeSpan.Zero ? Min(remaining, _options.Resilience.CircuitBreakerBreakDuration) : TimeSpan.Zero;
        }
    }

    private ResiliencePipeline BuildPipeline(KaleidoResilienceSettings resilience) =>
        new ResiliencePipelineBuilder { TimeProvider = _time }
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = resilience.CircuitBreakerFailureRatio,
                MinimumThroughput = resilience.CircuitBreakerMinimumThroughput,
                SamplingDuration = resilience.CircuitBreakerSamplingDuration,
                BreakDuration = resilience.CircuitBreakerBreakDuration,
                StateProvider = _circuit,
                OnOpened = args =>
                {
                    Interlocked.Exchange(ref _breakUntilTicks, (_time.GetUtcNow() + args.BreakDuration).UtcTicks);
                    LogCircuitOpened(_logger, args.BreakDuration);
                    return default;
                },
                OnClosed = _ =>
                {
                    LogCircuitClosed(_logger);
                    return default;
                },
            })
            .AddTimeout(resilience.AttemptTimeout)
            .Build();

    private static async Task WithGateAsync(SemaphoreSlim gate, Func<Task> action)
    {
        await gate.WaitAsync(CancellationToken.None);

        try
        {
            await action();
        }
        finally
        {
            gate.Release();
        }
    }

    private static KeyValuePair<string, object?> Tag(KaleidoOutboxEvent entry) =>
        new(KaleidoEventTelemetry.TagEventType, entry.EventType);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Event {EventType} ({EventId}) published.")]
    private static partial void LogPublished(ILogger logger, string eventType, Guid eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing event {EventType} ({EventId}) failed (attempt {Attempts}); retrying at {RetryAt}.")]
    private static partial void LogRetryScheduled(ILogger logger, Exception exception, string eventType, Guid eventId, int attempts, DateTimeOffset retryAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing event {EventType} ({EventId}) failed {Attempts} times; the event is parked in the outbox and will not be retried.")]
    private static partial void LogParked(ILogger logger, Exception exception, string eventType, Guid eventId, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event publishing paused for {BreakDuration}: too many publish attempts failed.")]
    private static partial void LogCircuitOpened(ILogger logger, TimeSpan breakDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event publishing resumed.")]
    private static partial void LogCircuitClosed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The event relay could not read or update the outbox; it tries again.")]
    private static partial void LogRelayFailed(ILogger logger, Exception exception);
}
