using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.Client;

// Keeps the host's copy of the control registry fresh (#553): both documents at startup, then each one when
// it is due, at the pace the registry hands out (the source's NextCheck; the retry while there is nothing to
// follow). A failed read is the source's to report (it keeps its last copy); the sync never stops for it.
internal sealed class CentralRegistrySync(
    ICentralRegistrySource source,
    TimeProvider time)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await source.SyncAsync(force: true, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(source.NextCheck, time, stoppingToken);
                await source.SyncAsync(force: false, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }
}
