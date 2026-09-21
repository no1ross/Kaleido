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

    public object? Response { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];
}

internal sealed class ProcessStepInvoker(
    IProcessObservability observability,
    IServiceScopeFactory scopeFactory)
    : IProcessStepInvoker
{

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
            observability.BeginHandler(
                new ProcessHandlerObservationDetails(
                    registration.Metadata.Name,
                    registration.Metadata.Version));

        using var scope =
            scopeFactory.CreateScope();

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
                    registration.GetResultFromTask,
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
        Func<Task, IProcessStepHandlerResult>? getResultFromTask,
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

        if (getResultFromTask is null)
        {
            throw new KaleidoFrameworkException(
                $"Handler '{handler.GetType().FullName}' has no cached result extractor.");
        }

        var handlerResult = getResultFromTask(task);

        return new ProcessStepInvokerResult
        {
            Succeeded = handlerResult.Succeeded,
            RequiredStep = handlerResult.RequiredStep,
            TargetProcessorName = handlerResult.TargetProcessorName,
            Response = handlerResult.Response,
            Messages = handlerResult.Messages
        };
    }
}