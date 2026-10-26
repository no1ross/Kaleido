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
        CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
internal sealed record StepInvocationResult
{
    public bool Succeeded { get; init; }

    public bool Rejected { get; init; }

    public Type? RequiredStep { get; init; }

    public InformationRequest? InformationRequest { get; init; }

    public string? TargetProcessorName { get; init; }

    public object? Response { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];
}

internal sealed class ProcessorStepInvoker(
    IProcessorObservability observability,
    IServiceScopeFactory scopeFactory,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IKaleidoInboundCallerAccessor inboundCaller)
    : IProcessStepInvoker
{

    public async Task<StepInvocationResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(processStep);
        ArgumentNullException.ThrowIfNull(context);

        using var handlerObservation =
            observability.BeginHandler(
                new ProcessorHandlerObservationDetails(
                    registration.Metadata.Name));

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
                    ExecutingStepName = registration.Metadata.Name,
                });

        // And the request's caller, so the handler's calls are made for that caller (ADR 0022, #428);
        // outside a request there is none, and its calls stay the service's own.
        if (inboundCaller.IsInboundRequest)
        {
            scope.ServiceProvider
                .GetRequiredService<IKaleidoInboundCallerAccessor>()
                .Set(inboundCaller.Principal, inboundCaller.Credential);
        }

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
        // A validation error is the caller's mistake, recorded once by the executor; not a handler failure.
        catch (Exception exception) when (exception is not OperationCanceledException and not KaleidoValidationException)
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
            Rejected = handlerResult.Rejected,
            RequiredStep = handlerResult.RequiredStep,
            InformationRequest = handlerResult.InformationRequest,
            TargetProcessorName = handlerResult.TargetProcessorName,
            Response = handlerResult.Response,
            Messages = handlerResult.Messages,
        };
    }
}
