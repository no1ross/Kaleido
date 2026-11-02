using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryRetentionSweeperTests
    : SutFixture
{
    private readonly Mock<IKaleidoRegistryStore> store = new();
    private readonly FakeTimeProvider time = new(Start);
    private readonly Mock<ILogger<RegistryRetentionSweeper>> logger = new();

    public RegistryRetentionSweeperTests() => logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private RegistryRetentionSweeper CreateSut()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryStore))).Returns(store.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new RegistryRetentionSweeper(factory.Object, new KaleidoRegistryOptions(), time, logger.Object);
    }

    private LogLevel[] Levels() =>
        [.. logger.Invocations.Where(x => x.Method.Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.Arguments[0])];

    [Fact]
    public async Task Sweep_RemovesWhatHasNotHeartbeatenForTheRetention_AndLogsIt()
    {
        store.Setup(x => x.RemoveInstancesSeenBeforeAsync(Start - TimeSpan.FromHours(24), It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await CreateSut().SweepAsync(CancellationToken.None);

        Assert.Equal([LogLevel.Information], Levels());
    }

    [Fact]
    public async Task Sweep_WithNothingToRemove_LogsNothing()
    {
        await CreateSut().SweepAsync(CancellationToken.None);

        Assert.Empty(Levels());
    }

    // A failing store is logged and tried again at the next period; the sweeper never stops.
    [Fact]
    public async Task Sweep_WhenTheStoreFails_LogsAWarning()
    {
        store.Setup(x => x.RemoveInstancesSeenBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down"));

        await CreateSut().SweepAsync(CancellationToken.None);

        Assert.Equal([LogLevel.Warning], Levels());
    }

    [Fact]
    public async Task Sweep_WhenTheHostStops_Propagates()
    {
        var canceled = new CancellationToken(canceled: true);
        store.Setup(x => x.RemoveInstancesSeenBeforeAsync(It.IsAny<DateTimeOffset>(), canceled)).ThrowsAsync(new OperationCanceledException(canceled));

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().SweepAsync(canceled));
    }

    // Running: a sweep every lease period, on the host's clock; stopping ends it.
    [Fact]
    public async Task Running_SweepsEveryLeasePeriod()
    {
        var swept = new TaskCompletionSource();
        store.Setup(x => x.RemoveInstancesSeenBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => swept.TrySetResult())
            .ReturnsAsync(0);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        time.Advance(new KaleidoRegistryOptions().Lease);
        await swept.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        store.Verify(x => x.RemoveInstancesSeenBeforeAsync(Start + TimeSpan.FromSeconds(30) - TimeSpan.FromHours(24), It.IsAny<CancellationToken>()), Times.Once);
    }
}
