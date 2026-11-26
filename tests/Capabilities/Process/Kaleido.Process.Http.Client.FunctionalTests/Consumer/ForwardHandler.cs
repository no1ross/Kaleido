namespace Kaleido.Process.Http.Client.FunctionalTests.Consumer;

public sealed class ForwardHandler(IKaleidoProcessorClientFactory clients) : IProcessStepHandler<ForwardStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(ForwardStep step, ProcessStepContext context, CancellationToken cancellationToken)
    {
        var target = await clients.GetClient("catalog", "2").ExecuteStepAsync(
            new ReviewOrderStep(step.Code), context.ProcessId, context.ProcessId.ToString("N"), cancellationToken);
        return ProcessStepHandlerResult.HandOff("catalog", target, nameof(ReviewOrderStep));
    }
}
