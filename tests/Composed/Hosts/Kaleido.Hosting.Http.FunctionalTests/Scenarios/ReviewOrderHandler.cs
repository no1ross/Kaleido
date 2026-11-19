namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

public sealed class ReviewOrderHandler : IProcessStepHandler<ReviewOrderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ReviewOrderStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
