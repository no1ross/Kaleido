using Kaleido.Process.Observability;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Participant.Execution;


internal sealed class ProcessStepInvoker : IProcessStepInvoker
{
    private readonly IProcessObservability _observability;
    private readonly IServiceScopeFactory _scopeFactory;

    public ProcessStepInvoker(
        IProcessObservability observability,
        IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _observability = observability;
        _scopeFactory = scopeFactory;
    }

    public async Task<ProcessStepInvokerResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(processStep);
        ArgumentNullException.ThrowIfNull(context);

        using var handlerObservation =
            _observability.BeginHandler(
                new ProcessHandlerObservationDetails(
                    registration.Metadata.Name,
                    registration.Metadata.Version));

        using var scope =
            _scopeFactory.CreateScope();

        var handler =
            scope.ServiceProvider.GetRequiredService(
                registration.HandlerType);

        try
        {
            var handlerResult =
                await ExecuteHandlerAsync(
                    handler,
                    processStep,
                    context,
                    cancellationToken);

            return handlerResult;
        }
        catch (Exception exception)
        {
            handlerObservation.HandlerFailed(exception);
            throw;
        }
    }

    private static async Task<ProcessStepInvokerResult> ExecuteHandlerAsync(
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
                ])
            ?? throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' returned null.");

        if (result is not Task task)
        {
            throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' returned an invalid result.");
        }

        await task;

        var taskResult =
            task.GetType()
                .GetProperty(nameof(Task<object>.Result))
                ?.GetValue(task)
            ?? throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' returned a null result.");

        if (taskResult is not IProcessStepHandlerResult handlerResult)
        {
            throw new InvalidOperationException(
                $"Handler '{handler.GetType().FullName}' returned an invalid handler result.");
        }

        return new ProcessStepInvokerResult
        {
            Succeeded = handlerResult.Succeeded,
            RequiredStep = handlerResult.RequiredStep,
            Response = handlerResult.Response!,
            Messages = handlerResult.Messages
        };
    }
}