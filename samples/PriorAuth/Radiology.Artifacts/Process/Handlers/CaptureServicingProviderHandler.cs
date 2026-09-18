using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.History.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureServicingProviderHandler(
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureServicingProviderStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServicingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        await historyClient.UpsertAsync(
            new UpsertPriorAuthRecordStep
            {
                ProcessorName = "radiology",
                Status = PriorAuthorizationStatus.Draft
            },
            context.ProcessId,
            cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
