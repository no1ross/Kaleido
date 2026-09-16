using Kaleido.Process.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Microsoft.Extensions.Logging;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class HistoryClient(
    IKaleidoProcessClientFactory processClientFactory,
    ILogger<HistoryClient> logger)
{
    public async Task UpsertAsync(
        UpsertPriorAuthRecordStep step,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await processClientFactory
                .GetClient("History")
                .ExecuteStepAsync(step, processId: null, cancellationToken);
        }
        catch (KaleidoProcessClientException ex)
        {
            logger.LogWarning(ex,
                "Failed to update history record for process {ProcessId}. Continuing.",
                step.ProcessId);
        }
    }
}
