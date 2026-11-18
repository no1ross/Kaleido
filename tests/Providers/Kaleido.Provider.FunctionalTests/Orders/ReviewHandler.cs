namespace Kaleido.Provider.FunctionalTests.Orders;

public sealed class ReviewHandler : IProcessStepHandler<ReviewStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ReviewStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
