using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Participant;

internal sealed class ProcessStepEngine : IProcessStepEngine
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProcessStepEngine(
        IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public async Task<ProcessStepResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var handler =
            scope.ServiceProvider.GetRequiredService(
                registration.HandlerType);

        return await ExecuteHandlerAsync(
            handler,
            processStep,
            context,
            cancellationToken);
    }

    private static async Task<ProcessStepResult> ExecuteHandlerAsync(
        object handler,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        var method =
            handler.GetType().GetMethod(
                nameof(IProcessStepHandler<object>.ExecuteAsync));

        return await (Task<ProcessStepResult>)method!.Invoke(
            handler,
            [
                processStep,
                context,
                cancellationToken
            ])!;
    }
}

