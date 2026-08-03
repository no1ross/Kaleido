using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Participant.Execution;


internal sealed class ProcessStepInvoker : IProcessStepInvoker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProcessStepInvoker(
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
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(processStep);
        ArgumentNullException.ThrowIfNull(context);

        using var scope =
            _scopeFactory.CreateScope();

        var handler =
            scope.ServiceProvider.GetRequiredService(
                registration.HandlerType);

        var handlerResult =
            await ExecuteHandlerAsync(
                handler,
                processStep,
                context,
                cancellationToken);

        return handlerResult;
    }

    private static async Task<ProcessStepResult> ExecuteHandlerAsync(
        object handler,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        
        var method =
            handler.GetType().GetMethod(
                nameof(IProcessStepHandler<object>.ExecuteAsync))
            ?? throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' does not expose ExecuteAsync.");

        var result =
            method.Invoke(
                handler,
                [
                    processStep,
                    context,
                    cancellationToken
                ]);

        if (result is not Task<ProcessStepResult> task)
        {
            throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' returned an invalid result.");
        }

        return await task;
    }
}