namespace Kaleido.Process.Http.Client.FunctionalTests.Publisher;

public sealed class ReviewOrderHandler(ExecutionCounter executions) : IProcessStepHandler<ReviewOrderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ReviewOrderStep step, ProcessStepContext context, CancellationToken cancellationToken)
    {
        executions.Increment();
        return Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
