using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.Client;

// Keeps the host's copy of the control registry fresh (#553): both documents at startup, then a check
// every AvailabilityInterval, the capabilities only when theirs is due. A failed read is the source's to
// report (it keeps its last copy); the sync never stops for it.
internal sealed class CentralRegistrySync(
    ICentralRegistrySource source,
    KaleidoRegistryClientOptions options,
    TimeProvider time)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await source.SyncAsync(force: true, stoppingToken);

            using var timer = new PeriodicTimer(options.AvailabilityInterval, time);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await source.SyncAsync(force: false, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }
}
