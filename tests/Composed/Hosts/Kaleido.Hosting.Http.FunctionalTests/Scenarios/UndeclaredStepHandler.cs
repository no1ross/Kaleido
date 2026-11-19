namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

public sealed class UndeclaredStepHandler : IProcessStepHandler<UndeclaredStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(UndeclaredStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
