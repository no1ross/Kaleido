using Kaleido.Exceptions;
using Kaleido.Process.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Execution;

public interface IProcessStepInvoker
{
    Task<ProcessStepInvokerResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessStepInvokerResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public object Response { get; init; } = null!;

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];
}

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
            ?? throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' does not expose ExecuteAsync.");

        var result =
            method.Invoke(
                handler,
                [
                    processStep,
                context,
                cancellationToken
                ])
            ?? throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' returned null.");

        if (result is not Task task)
        {
            throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' returned an invalid result.");
        }

        await task;

        var taskResult =
            task.GetType()
                .GetProperty(nameof(Task<object>.Result))
                ?.GetValue(task)
            ?? throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' returned a null result.");

        if (taskResult is not IProcessStepHandlerResult handlerResult)
        {
            throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' returned an invalid handler result.");
        }

        return new ProcessStepInvokerResult
        {
            Succeeded = handlerResult.Succeeded,
            RequiredStep = handlerResult.RequiredStep,
            TargetProcessorName = handlerResult.TargetProcessorName,
            Response = handlerResult.Response!,
            Messages = handlerResult.Messages
        };
    }
}