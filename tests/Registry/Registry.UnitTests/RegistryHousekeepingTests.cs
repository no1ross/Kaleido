using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Kaleido.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Registry.UnitTests;

// #553: a registry node keeps the registry's watch alive, and sweeps only once the watch has run unbroken
// for the retention period.
public sealed class RegistryHousekeepingTests
    : SutFixture
{
    private static readonly KaleidoRegistryOptions Options = new();

    private readonly Mock<IKaleidoRegistryStore> store = new();
    private readonly FakeTimeProvider time = new(Start);
    private readonly Mock<ILogger<RegistryHousekeeping>> logger = new();
    private DateTimeOffset resumed = Start - Options.Retention;

    public RegistryHousekeepingTests()
    {
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        store.Setup(x => x.TouchWatchAsync(It.IsAny<DateTimeOffset>(), Options.Lease, It.IsAny<CancellationToken>())).ReturnsAsync(() => resumed);
    }

    private RegistryHousekeeping CreateSut()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryStore))).Returns(store.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new RegistryHousekeeping(factory.Object, Options, time, logger.Object);
    }

    private LogLevel[] Levels() =>
        [.. logger.Invocations.Where(x => x.Method.Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.Arguments[0])];

    private void VerifySweeps(Times times) =>
        store.Verify(x => x.RemoveInstancesSeenBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Tick_TouchesTheWatch_AndSweepsWhatHasNotHeartbeatenForTheRetention()
    {
        store.Setup(x => x.RemoveInstancesSeenBeforeAsync(Start - Options.Retention, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await CreateSut().TickAsync(CancellationToken.None);

        store.Verify(x => x.TouchWatchAsync(Start, Options.Lease, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([LogLevel.Information], Levels());
    }

    // The sweep waits until the watch has run unbroken for the retention: silence while no node was up
    // is never counted.
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    public async Task Tick_SweepsOnlyOnceTheWatchHasRunForTheRetention(long ticksFromTheRetention, int sweeps)
    {
        resumed = Start - (Options.Retention + TimeSpan.FromTicks(ticksFromTheRetention));

        await CreateSut().TickAsync(CancellationToken.None);

        VerifySweeps(Times.Exactly(sweeps));
    }

    // Touches come three times a lease; sweeps once a lease.
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    public async Task Tick_SweepsOncePerLease(long ticksFromTheLease, int sweeps)
    {
        var sut = CreateSut();

        await sut.TickAsync(CancellationToken.None);
        time.Advance(Options.Lease + TimeSpan.FromTicks(ticksFromTheLease));
        await sut.TickAsync(CancellationToken.None);

        VerifySweeps(Times.Exactly(sweeps));
        store.Verify(x => x.TouchWatchAsync(It.IsAny<DateTimeOffset>(), Options.Lease, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Tick_WithNothingToRemove_LogsNothing()
    {
        await CreateSut().TickAsync(CancellationToken.None);

        Assert.Empty(Levels());
    }

    // A failing store is logged and tried again at the next touch; housekeeping never stops.
    [Fact]
    public async Task Tick_WhenTheStoreFails_LogsAWarning()
    {
        store.Setup(x => x.TouchWatchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down"));

        await CreateSut().TickAsync(CancellationToken.None);

        Assert.Equal([LogLevel.Warning], Levels());
        VerifySweeps(Times.Never());
    }

    [Fact]
    public async Task Tick_WhenTheHostStops_Propagates()
    {
        var canceled = new CancellationToken(canceled: true);
        store.Setup(x => x.TouchWatchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), canceled)).ThrowsAsync(new OperationCanceledException(canceled));

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().TickAsync(canceled));
    }

    [Fact]
    public void TheTouches_AreThreePerLease()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), CreateSut().TouchInterval);
    }

    // Running: a touch at startup, then every lease / 3 on the host's clock; stopping ends it.
    [Fact]
    public async Task Running_TouchesAtStartup_AndEveryTouchInterval()
    {
        var touches = 0;
        var second = new TaskCompletionSource();
        store.Setup(x => x.TouchWatchAsync(It.IsAny<DateTimeOffset>(), Options.Lease, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref touches) == 2)
                {
                    second.TrySetResult();
                }
            })
            .ReturnsAsync(() => resumed);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(10));
        await second.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        store.Verify(x => x.TouchWatchAsync(Start, Options.Lease, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.TouchWatchAsync(Start + TimeSpan.FromSeconds(10), Options.Lease, It.IsAny<CancellationToken>()), Times.Once);
    }
}
