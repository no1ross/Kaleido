using Kaleido.Processor.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventRelayTests
    : SutFixture
{
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    // The relay's clock: leases, retries, timeouts and the circuit's break move only when a test advances it.
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private KaleidoEventRelay CreateSut(
        IKaleidoOutboxStore store,
        IEventPublisher publisher,
        KaleidoEventOptions? options = null,
        IKaleidoEventSignal? signal = null,
        Microsoft.Extensions.Logging.ILogger<KaleidoEventRelay>? logger = null,
        TimeProvider? clock = null)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoOutboxStore))).Returns(store);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new KaleidoEventRelay(
            scopeFactory.Object,
            publisher,
            signal ?? Mock.Of<IKaleidoEventSignal>(),
            new KaleidoServiceOptions { ServiceName = "svc", Events = options ?? new KaleidoEventOptions() },
            clock ?? time,
            logger ?? NullLogger<KaleidoEventRelay>.Instance);
    }

    private static Mock<Microsoft.Extensions.Logging.ILogger<KaleidoEventRelay>> EnabledLogger()
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<KaleidoEventRelay>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);
        return logger;
    }

    private static void AssertLogged(
        Mock<Microsoft.Extensions.Logging.ILogger<KaleidoEventRelay>> logger,
        Microsoft.Extensions.Logging.LogLevel level,
        string fragment) =>
        Assert.Contains(
            logger.Invocations,
            x => x.Method.Name == nameof(Microsoft.Extensions.Logging.ILogger.Log) &&
                 (Microsoft.Extensions.Logging.LogLevel)x.Arguments[0] == level &&
                 x.Arguments[2].ToString()!.Contains(fragment, StringComparison.Ordinal));

    private static KaleidoOutboxEvent Entry(int attempts = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.ProcessCreated,
            Context = new ProcessEventContext { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" },
            Event = new ProcessCreated { OccurredOn = now, State = ProcessExecutionState.Active, CreatedUtc = now, UpdatedUtc = now, SubmittedStepCount = 1 },
        }.ToOutboxEvent();

        return entry with { Attempts = attempts };
    }

    private static Mock<IKaleidoOutboxStore> StoreClaiming(params KaleidoOutboxEvent[] entries)
    {
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        return store;
    }

    private static Mock<IEventPublisher> Publisher(Func<Task>? publish = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(), It.IsAny<CancellationToken>()))
            .Returns(() => publish?.Invoke() ?? Task.CompletedTask);

        return publisher;
    }

    private static Mock<IEventPublisher> FailingPublisher() =>
        Publisher(() => Task.FromException(new InvalidOperationException("broker down")));

    [Fact]
    public async Task RelayBatchAsync_PublishesEachClaimedEntryAndRemovesIt()
    {
        var first = Entry();
        var second = Entry();
        var store = StoreClaiming(first, second);
        var publisher = Publisher();

        var claimed = await CreateSut(store.Object, publisher.Object).RelayBatchAsync(CancellationToken.None);

        Assert.Equal(2, claimed);
        publisher.Verify(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(x => x.CompleteEventAsync(first.EventId, It.IsAny<CancellationToken>()));
        store.Verify(x => x.CompleteEventAsync(second.EventId, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task RelayBatchAsync_ClaimsAtMostTheBatchSizeWithTheDerivedLease()
    {
        var store = StoreClaiming();
        var options = new KaleidoEventOptions { BatchSize = 7 };
        var sut = CreateSut(store.Object, Publisher().Object, options);

        await sut.RelayBatchAsync(CancellationToken.None);

        store.Verify(x => x.ClaimEventsAsync(7, sut.Lease, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void Lease_CoversAWholeBatchAtTheConfiguredParallelismPlusAMargin()
    {
        var options = new KaleidoEventOptions { BatchSize = 20, MaxConcurrentPublishes = 4 };
        options = options with { Resilience = options.Resilience with { AttemptTimeout = TimeSpan.FromSeconds(10) } };

        var sut = CreateSut(Mock.Of<IKaleidoOutboxStore>(), Publisher().Object, options);

        Assert.Equal(TimeSpan.FromSeconds((5 * 10) + 30), sut.Lease);
    }

    [Fact]
    public async Task RelayBatchAsync_WhenPublishingFails_SchedulesARetryWithBackoff()
    {
        var entry = Entry();
        var store = StoreClaiming(entry);
        var options = new KaleidoEventOptions();

        await CreateSut(store.Object, FailingPublisher().Object, options).RelayBatchAsync(CancellationToken.None);

        store.Verify(x => x.FailEventAsync(
            entry.EventId,
            time.GetUtcNow() + KaleidoEventRelay.RetryDelay(options.Resilience.RetryDelay, 1),
            "broker down",
            It.IsAny<CancellationToken>()));
        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The last retry is still scheduled; the attempt after it parks the entry.
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task RelayBatchAsync_AroundTheLastRetry(int attemptsBefore, bool parked)
    {
        var options = new KaleidoEventOptions();
        options = options with { Resilience = options.Resilience with { MaxRetryAttempts = 2 } };
        var entry = Entry(attempts: attemptsBefore);
        var store = StoreClaiming(entry);

        await CreateSut(store.Object, FailingPublisher().Object, options).RelayBatchAsync(CancellationToken.None);

        store.Verify(x => x.FailEventAsync(entry.EventId, It.Is<DateTimeOffset?>(retryAt => (retryAt == null) == parked), "broker down", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task RelayBatchAsync_WhenThePublisherHangs_TheAttemptTimesOutAndFails()
    {
        var options = new KaleidoEventOptions();
        options = options with { Resilience = options.Resilience with { AttemptTimeout = TimeSpan.FromSeconds(10) } };
        var entry = Entry();
        var store = StoreClaiming(entry);
        var publishing = new TaskCompletionSource();
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(), It.IsAny<CancellationToken>()))
            .Returns<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>, CancellationToken>((_, token) =>
            {
                publishing.TrySetResult();
                return Task.Delay(Timeout.Infinite, token);
            });

        var relaying = CreateSut(store.Object, publisher.Object, options).RelayBatchAsync(CancellationToken.None);
        await publishing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await relaying.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        store.Verify(x => x.FailEventAsync(entry.EventId, It.IsNotNull<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
    }

    // An entry is attempted only while a whole attempt fits in the batch's lease (two rounds of 10 s
    // plus the 30 s margin = 50 s): after a first publish that took 40 s the second still fits, one
    // tick later it is left for the next claim.
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    public async Task RelayBatchAsync_AttemptsAnEntryOnlyWhileAWholeAttemptFitsInTheLease(int ticksOver, int published)
    {
        var options = new KaleidoEventOptions { BatchSize = 2, MaxConcurrentPublishes = 1 };
        options = options with { Resilience = options.Resilience with { AttemptTimeout = TimeSpan.FromSeconds(10) } };
        var store = StoreClaiming(Entry(), Entry());
        var publisher = Publisher(() =>
        {
            time.Advance(TimeSpan.FromSeconds(40) + (Tick * ticksOver));
            return Task.CompletedTask;
        });

        await CreateSut(store.Object, publisher.Object, options).RelayBatchAsync(CancellationToken.None);

        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(published));
    }

    [Fact]
    public async Task RelayBatchAsync_WhenTheCircuitOpens_StopsAttemptingWithoutBurningAttempts()
    {
        var options = new KaleidoEventOptions { BatchSize = 5 };
        options = options with { Resilience = options.Resilience with { CircuitBreakerMinimumThroughput = 2 } };
        var store = StoreClaiming(Entry(), Entry(), Entry(), Entry(), Entry());
        var publisher = FailingPublisher();

        await CreateSut(store.Object, publisher.Object, options).RelayBatchAsync(CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(x => x.FailEventAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RelayBatchAsync_RunsAtMostTheConfiguredPublishesAtOnce()
    {
        var options = new KaleidoEventOptions { BatchSize = 6, MaxConcurrentPublishes = 2 };
        var store = StoreClaiming(Entry(), Entry(), Entry(), Entry(), Entry(), Entry());
        var running = 0;
        var maxRunning = 0;
        var publisher = Publisher(async () =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            await Task.Delay(30);
            Interlocked.Decrement(ref running);
        });

        await CreateSut(store.Object, publisher.Object, options).RelayBatchAsync(CancellationToken.None);

        Assert.InRange(maxRunning, 1, 2);
        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task RelayBatchAsync_WhenTheStoreFails_ReportsNothingClaimedAndDoesNotThrow()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        Assert.Equal(0, await CreateSut(store.Object, Publisher().Object).RelayBatchAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 16)]
    public void RetryDelay_DoublesPerAttempt(int attempts, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), KaleidoEventRelay.RetryDelay(TimeSpan.FromSeconds(1), attempts));
    }

    [Fact]
    public void RetryDelay_IsCappedAtFiveMinutes()
    {
        Assert.Equal(KaleidoEventRelay.MaxRetryDelay, KaleidoEventRelay.RetryDelay(TimeSpan.FromSeconds(1), 50));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAPublisher_ClaimsNothing()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        using var sut = CreateSut(store.Object, Mock.Of<INoOpEventPublisher>());

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhileTheOutboxIsEmpty_BacksOffUpToTheIdleInterval()
    {
        var waits = new List<TimeSpan>();
        var enoughWaits = new TaskCompletionSource();
        var signal = new Mock<IKaleidoEventSignal>();
        signal
            .Setup(x => x.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(async (wait, token) =>
            {
                lock (waits)
                {
                    waits.Add(wait);
                }

                if (waits.Count < 5)
                {
                    return false;
                }

                enoughWaits.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return false;
            });
        using var sut = CreateSut(StoreClaiming().Object, Publisher().Object, signal: signal.Object);

        await sut.StartAsync(CancellationToken.None);
        await enoughWaits.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), KaleidoEventRelay.IdleInterval, KaleidoEventRelay.IdleInterval],
            waits.Take(5));
    }

    [Fact]
    public async Task ExecuteAsync_AfterAFullBatch_ClaimsAgainWithoutWaiting()
    {
        var options = new KaleidoEventOptions { BatchSize = 1 };
        var claims = 0;
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref claims) == 1 ? [Entry()] : []);
        var waited = new TaskCompletionSource();
        var signal = new Mock<IKaleidoEventSignal>();
        signal
            .Setup(x => x.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(async (_, token) =>
            {
                waited.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return false;
            });
        using var sut = CreateSut(store.Object, Publisher().Object, options, signal.Object);

        await sut.StartAsync(CancellationToken.None);
        await waited.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.StopAsync(CancellationToken.None);

        // The full batch was followed by a second claim before the first wait.
        Assert.Equal(2, claims);
    }

    [Fact]
    public async Task RelayBatchAsync_LogsEachOutcomeAtItsLevel()
    {
        var published = Entry();
        var logger = EnabledLogger();

        await CreateSut(StoreClaiming(published).Object, Publisher().Object, logger: logger.Object).RelayBatchAsync(CancellationToken.None);
        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Debug, $"({published.EventId}) published.");

        var retried = Entry();
        await CreateSut(StoreClaiming(retried).Object, FailingPublisher().Object, logger: logger.Object).RelayBatchAsync(CancellationToken.None);
        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Warning, $"({retried.EventId}) failed (attempt 1); retrying at");

        var options = new KaleidoEventOptions();
        options = options with { Resilience = options.Resilience with { MaxRetryAttempts = 0 } };
        var parked = Entry();
        await CreateSut(StoreClaiming(parked).Object, FailingPublisher().Object, options, logger: logger.Object).RelayBatchAsync(CancellationToken.None);
        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Error, $"({parked.EventId}) failed 1 times; the event is parked");
    }

    [Fact]
    public async Task RelayBatchAsync_WhenTheStoreFails_LogsAnError()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));
        var logger = EnabledLogger();

        await CreateSut(store.Object, Publisher().Object, logger: logger.Object).RelayBatchAsync(CancellationToken.None);

        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Error, "could not read or update the outbox");
    }

    [Fact]
    public async Task RelayBatchAsync_WhenCanceled_PropagatesTheCancellation()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(new CancellationToken(canceled: true)));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateSut(store.Object, Publisher().Object).RelayBatchAsync(new CancellationToken(canceled: true)));
    }

    // Only the host stopping is a cancellation (#517): a store timeout is a failed batch, logged.
    [Fact]
    public async Task RelayBatchAsync_WhenTheStoreTimesOut_RelaysNothing_AndDoesNotThrow()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"));

        Assert.Equal(0, await CreateSut(store.Object, Publisher().Object).RelayBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RelayBatchAsync_WhenTheHostStopsDuringAPublish_DoesNotCountAFailedAttempt()
    {
        using var stopping = new CancellationTokenSource();
        var store = StoreClaiming(Entry());
        var publisher = Publisher(async () =>
        {
            await stopping.CancelAsync();
            stopping.Token.ThrowIfCancellationRequested();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateSut(store.Object, publisher.Object).RelayBatchAsync(stopping.Token));

        store.Verify(x => x.FailEventAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RelayBatchAsync_WhenTheCircuitOpens_LogsThePauseAndLeavesTheRestUntouched()
    {
        var options = new KaleidoEventOptions { BatchSize = 3 };
        options = options with { Resilience = options.Resilience with { CircuitBreakerMinimumThroughput = 2 } };
        var logger = EnabledLogger();
        var store = StoreClaiming(Entry(), Entry(), Entry());

        await CreateSut(store.Object, FailingPublisher().Object, options, logger: logger.Object).RelayBatchAsync(CancellationToken.None);

        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Warning, "Event publishing paused for");
        store.Verify(x => x.FailEventAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Paused until the break ends: one tick before it nothing is attempted, at its end the next
    // attempt closes the circuit again.
    [Fact]
    public async Task RelayBatchAsync_WhenTheCircuitOpensAndLaterRecovers_LogsPauseAndResume()
    {
        var breakDuration = TimeSpan.FromSeconds(30);
        var options = new KaleidoEventOptions { BatchSize = 2 };
        options = options with { Resilience = options.Resilience with { CircuitBreakerMinimumThroughput = 2, CircuitBreakerBreakDuration = breakDuration } };
        var failing = true;
        var publisher = Publisher(() => failing ? Task.FromException(new InvalidOperationException("broker down")) : Task.CompletedTask);
        var logger = EnabledLogger();
        var store = StoreClaiming(Entry(), Entry());
        var sut = CreateSut(store.Object, publisher.Object, options, logger: logger.Object);

        await sut.RelayBatchAsync(CancellationToken.None);
        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Warning, "Event publishing paused for");
        failing = false;

        time.Advance(breakDuration - Tick);
        await sut.RelayBatchAsync(CancellationToken.None);
        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        time.Advance(Tick);
        await sut.RelayBatchAsync(CancellationToken.None);

        AssertLogged(logger, Microsoft.Extensions.Logging.LogLevel.Warning, "Event publishing resumed.");
        store.Verify(x => x.CompleteEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // The relay waits out the break and claims again exactly when it ends: once it waits (its timer
    // for the break exists), the clock is moved to one tick before the end, then to the end, and the
    // second claim happens at that instant.
    [Fact]
    public async Task ExecuteAsync_WhileTheCircuitIsOpen_WaitsTheBreakDurationBeforeClaimingAgain()
    {
        var breakDuration = TimeSpan.FromSeconds(45);
        var clock = new TimerObservingClock(time.GetUtcNow(), breakDuration);
        var options = new KaleidoEventOptions { BatchSize = 2 };
        options = options with { Resilience = options.Resilience with { CircuitBreakerMinimumThroughput = 2, CircuitBreakerBreakDuration = breakDuration } };
        var claimTimes = new List<DateTimeOffset>();
        var store = new Mock<IKaleidoOutboxStore>();
        store
            .Setup(x => x.ClaimEventsAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                lock (claimTimes)
                {
                    claimTimes.Add(clock.GetUtcNow());
                    return claimTimes.Count == 1 ? [Entry(), Entry()] : [];
                }
            });
        var failures = 0;
        var opened = new TaskCompletionSource();
        store
            .Setup(x => x.FailEventAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref failures) == 2)
                {
                    opened.TrySetResult();
                }

                return Task.CompletedTask;
            });
        var waited = new TaskCompletionSource();
        var signal = new Mock<IKaleidoEventSignal>();
        signal
            .Setup(x => x.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(async (_, token) =>
            {
                waited.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return false;
            });
        using var sut = CreateSut(store.Object, FailingPublisher().Object, options, signal.Object, clock: clock);

        await sut.StartAsync(CancellationToken.None);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await clock.Waiting.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        clock.Advance(breakDuration - Tick);
        Assert.Single(claimTimes);
        clock.Advance(Tick);
        await waited.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(2, claimTimes.Count);
        Assert.Equal(breakDuration, claimTimes[1] - claimTimes[0]);
    }

    [Fact]
    public async Task ExecuteAsync_AfterAPartialBatch_WaitsTheRelayIntervalAndStopsWhenTheHostStops()
    {
        var options = new KaleidoEventOptions { BatchSize = 5 };
        var store = StoreClaiming(Entry());
        var waits = new List<TimeSpan>();
        var waited = new TaskCompletionSource();
        var signal = new Mock<IKaleidoEventSignal>();
        signal
            .Setup(x => x.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(async (wait, token) =>
            {
                lock (waits)
                {
                    waits.Add(wait);
                }

                waited.TrySetResult();
                await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }, TaskScheduler.Default);
                return false;
            });
        using var sut = CreateSut(store.Object, Publisher().Object, options, signal.Object);

        await sut.StartAsync(CancellationToken.None);
        await waited.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.StopAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        Assert.Equal(KaleidoEventRelay.RelayInterval, Assert.Single(waits));
        Assert.True(sut.ExecuteTask!.IsCompletedSuccessfully);
    }

    // A fake clock that reports when a timer of the given length is created: the relay's wait for the
    // circuit's break (the attempt timeouts have another length).
    private sealed class TimerObservingClock(DateTimeOffset start, TimeSpan observed) : FakeTimeProvider(start)
    {
        private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Waiting => _waiting.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);

            if (dueTime == observed)
            {
                _waiting.TrySetResult();
            }

            return timer;
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;

        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
