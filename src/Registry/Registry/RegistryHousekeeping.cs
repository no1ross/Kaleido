using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Registry;

// Each registry node's housekeeping (#553), on the store every node shares:
// - it keeps the registry's watch alive: a touch at startup and every lease / 3, so while any node is up
//   the watch is unbroken (a node's own uptime does not matter), and after every node was down it resumes;
// - every lease, it removes the instances with no heartbeat for the retention period, and the registrations
//   no instance runs any more - but only once the watch has run unbroken for the retention period, so time
//   when no node was listening never counts as an instance's silence. Several nodes sweeping one store
//   remove the same rows, which is harmless.
internal sealed partial class RegistryHousekeeping(
    IServiceScopeFactory scopeFactory,
    KaleidoRegistryOptions options,
    TimeProvider time,
    ILogger<RegistryHousekeeping> logger)
    : BackgroundService
{
    private DateTimeOffset? _lastSweep;

    // Three touches per lease: one late touch never breaks the watch.
    internal TimeSpan TouchInterval => options.Lease / 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await TickAsync(stoppingToken);

            using var timer = new PeriodicTimer(TouchInterval, time);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>();
            var now = time.GetUtcNow();
            var resumed = await store.TouchWatchAsync(now, options.Lease, cancellationToken);

            if (now - resumed < options.Retention || (_lastSweep is { } last && now - last < options.Lease))
            {
                return;
            }

            _lastSweep = now;
            var removed = await store.RemoveInstancesSeenBeforeAsync(now - options.Retention, cancellationToken);

            if (removed > 0)
            {
                LogRemoved(logger, removed, options.Retention);
            }
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogHousekeepingFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The control registry removed {Count} instance(s) with no heartbeat for {Retention}.")]
    private static partial void LogRemoved(ILogger logger, int count, TimeSpan retention);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry could not keep its watch or remove its expired instances; it tries again at the next touch.")]
    private static partial void LogHousekeepingFailed(ILogger logger, Exception exception);
}
