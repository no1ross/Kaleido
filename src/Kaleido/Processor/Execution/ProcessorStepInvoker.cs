using Kaleido.Processor.Observability;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Processor.Execution;

internal interface IProcessStepInvoker
{
    Task<StepInvocationResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

[ExcludeFromCodeCoverage]
internal sealed record StepInvocationResult
{
    public bool Succeeded { get; init; }

    public Type? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public object? Response { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];
}

internal sealed class ProcessorStepInvoker(
    IProcessorObservability observability,
    IServiceScopeFactory scopeFactory,
    IKaleidoCorrelationContextAccessor correlationAccessor)
    : IProcessStepInvoker
{

    public async Task<StepInvocationResult> ExecuteAsync(
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
                new ProcessorHandlerObservationDetails(
                    registration.Metadata.Name,
                    registration.Metadata.Version));

        using var scope =
            scopeFactory.CreateScope();

        // The handler runs in its own scope, whose scoped correlation would otherwise be
        // empty: carry the request's correlation in, with this process's id and the
        // executing step, so the handler's outbound calls forward RequestId/ProcessId and
        // stamp this processor + step as the caller.
        scope.ServiceProvider
            .GetRequiredService<IKaleidoCorrelationContextInitializer>()
            .Initialize(
                correlationAccessor.Current with
                {
                    ProcessId = context.ProcessId,
                    ExecutingStepName = registration.Metadata.Name
                });

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
                    registration,
                    cancellationToken);

            return handlerResult;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            handlerObservation.HandlerFailed(exception);
            throw;
        }
    }

    private static async Task<StepInvocationResult> ExecuteHandlerAsync(
        object handler,
        object processStep,
        ProcessStepContext context,
        ProcessStepRegistration registration,
        CancellationToken cancellationToken)
    {
        if (registration.InvokeHandlerAsync is null)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Handler '{handler.GetType().FullName}' has no cached invoker.");
        }

        var task =
            registration.InvokeHandlerAsync(
                handler,
                processStep,
                context,
                cancellationToken)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.InvalidHandlerResult,
                $"Handler '{handler.GetType().FullName}' returned null.");

        await task;

        if (registration.GetResultFromTask is null)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.InvalidHandlerResult,
                $"Handler '{handler.GetType().FullName}' has no cached result extractor.");
        }

        var handlerResult = registration.GetResultFromTask(task);

        return new StepInvocationResult
        {
            Succeeded = handlerResult.Succeeded,
            RequiredStep = handlerResult.RequiredStep,
            TargetProcessorName = handlerResult.TargetProcessorName,
            Response = handlerResult.Response,
            Messages = handlerResult.Messages
        };
    }
}
