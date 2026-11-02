using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Registry;

// Removes the instances that have not sent a heartbeat for the retention period, and the registrations no
// instance runs any more (#553). It runs every lease period; several registry instances sweeping one store
// remove the same rows, which is harmless.
internal sealed partial class RegistryRetentionSweeper(
    IServiceScopeFactory scopeFactory,
    KaleidoRegistryOptions options,
    TimeProvider time,
    ILogger<RegistryRetentionSweeper> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Lease, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var removed = await scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>()
                .RemoveInstancesSeenBeforeAsync(time.GetUtcNow() - options.Retention, cancellationToken);

            if (removed > 0)
            {
                LogRemoved(logger, removed, options.Retention);
            }
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogSweepFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The control registry removed {Count} instance(s) with no heartbeat for {Retention}.")]
    private static partial void LogRemoved(ILogger logger, int count, TimeSpan retention);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry could not remove its expired instances; it tries again at the next lease period.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
