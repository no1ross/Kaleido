using Kaleido.Http.Client;
using Kaleido.Http.Client.Process;
using Kaleido.Http.Process;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Microsoft.Extensions.Logging;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class HistoryClient(
    IKaleidoProcessClientFactory processClientFactory,
    ILogger<HistoryClient> logger)
{
    public async Task UpsertAsync(
        UpsertPriorAuthRecordStep step,
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await processClientFactory
                .GetClient("History")
                .ExecuteStepAsync(step, cancellationToken);
        }
        catch (KaleidoHttpClientException ex)
        {
            logger.LogWarning(ex,
                "Failed to update history record for process {ProcessId}. Continuing.",
                processId);
        }
    }
}
