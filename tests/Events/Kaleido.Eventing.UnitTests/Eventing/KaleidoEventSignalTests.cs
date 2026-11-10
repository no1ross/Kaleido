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
    public async Task Notify_ConcurrentlyFromSeveralThreads_NeverThrowsAndWakesOnce()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            using var sut = CreateSut();
            using var start = new Barrier(4);
            var threads = Enumerable.Range(0, 4)
                .Select(_ => new Thread(() =>
                {
                    start.SignalAndWait();
                    sut.Notify();
                }))
                .ToArray();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.True(await sut.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
            Assert.False(await sut.WaitAsync(TimeSpan.Zero, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Notify_RacingNotifiersWhileTheRelayDrains_NeverThrow()
    {
        using var sut = CreateSut();
        using var stop = new CancellationTokenSource();
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        void Notifier()
        {
            for (var i = 0; i < 200_000; i++)
            {
                try
                {
                    sut.Notify();
                }
                catch (Exception exception)
                {
                    failures.Enqueue(exception);
                }
            }
        }

        var drainer = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await sut.WaitAsync(TimeSpan.Zero, CancellationToken.None);
            }
        });

        await Task.WhenAll(Task.Run(Notifier), Task.Run(Notifier), Task.Run(Notifier));
        await stop.CancelAsync();
        await drainer;

        Assert.Empty(failures);
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
