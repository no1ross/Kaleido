using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventSignalTests
    : SutFixture
{
    private static KaleidoEventSignal CreateSut() => new();

    [Fact]
    public async Task WaitAsync_AfterANotification_ReturnsAtOnce()
    {
        using var sut = CreateSut();

        sut.Notify();

        Assert.True(await sut.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_WithoutANotification_TimesOut()
    {
        using var sut = CreateSut();

        Assert.False(await sut.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
    }

    [Fact]
    public async Task Notify_SeveralTimesBeforeTheWait_WakesTheRelayOnce()
    {
        using var sut = CreateSut();

        sut.Notify();
        sut.Notify();
        sut.Notify();

        Assert.True(await sut.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.False(await sut.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_Throws()
    {
        using var sut = CreateSut();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token));
    }
}
