using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Client.UnitTests;

// #553: the host's copy of the control registry is read at startup, then checked when the source says - at
// the registry's pace.
public sealed class CentralRegistrySyncTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<ICentralRegistrySource> source = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

    private CentralRegistrySync CreateSut() => new(source.Object, time);

    [Fact]
    public async Task Running_ReadsBothAtStartup_ThenChecksWhenTheSourceSays()
    {
        var checkedOnce = new TaskCompletionSource();
        source.Setup(x => x.NextCheck).Returns(TimeSpan.FromSeconds(7));
        source.Setup(x => x.SyncAsync(false, It.IsAny<CancellationToken>())).Callback(() => checkedOnce.TrySetResult()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(7) - TimeSpan.FromTicks(1));
        source.Verify(x => x.SyncAsync(false, It.IsAny<CancellationToken>()), Times.Never);
        time.Advance(TimeSpan.FromTicks(1));
        await checkedOnce.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        source.Verify(x => x.SyncAsync(true, It.IsAny<CancellationToken>()), Times.Once);
        source.Verify(x => x.SyncAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stopping_EndsTheChecks()
    {
        source.Setup(x => x.NextCheck).Returns(TimeSpan.FromSeconds(7));
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        source.Verify(x => x.SyncAsync(false, It.IsAny<CancellationToken>()), Times.Never);
    }
}
