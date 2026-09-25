using Kaleido.Process;
using Kaleido.Process.Registry;

namespace Kaleido.Http.Process.Contracts;

// Wire shapes (ProcessExecutionResponse, StepExecutionResponse, etc.)
// are defined in Kaleido.Http.Abstractions.
// Factory methods and mapping logic that depend on server-side types live here.

internal interface IProcessExecutionResponseFactory
{
    ProcessExecutionResponse CreateExecutionResponse(
        ProcessResult processResult,
        IProcessStepRegistry registry,
        string serviceName);

    StepExecutionResponse CreateStepResponse(
        ProcessResult processResult,
        ProcessStepResult stepResult,
        IProcessStepRegistry registry,
        string serviceName);

    StepExecutionResponse<TResponse> CreateStepResponse<TResponse>(
        ProcessResult processResult,
        ProcessStepResult stepResult,
        IProcessStepRegistry registry,
        string serviceName);
}

internal sealed class ProcessExecutionResponseFactory(
    IProcessResponseFactory responseFactory)
    : IProcessExecutionResponseFactory
{
    public ProcessExecutionResponse CreateExecutionResponse(
        ProcessResult processResult,
        IProcessStepRegistry registry,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(registry);

        return new ProcessExecutionResponse
        {
            ProcessId =
                processResult.ProcessId,

            // RequiredStep is null when TargetProcessorName is set —
            // consumer must call the target processor's state endpoint instead.
            RequiredStep =
                processResult.TargetProcessorName is null
                    ? processResult.RequiredStep
                    : null,

            TargetProcessorName =
                processResult.TargetProcessorName,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(stepName =>
                        {
                            var registration = registry.Find(stepName)
                                ?? throw new KaleidoFrameworkException(
                                    FrameworkErrorCodes.MissingRegistration,
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return responseFactory.CreateStepSummary(
                                ToSummary(registration),
                                serviceName);
                        })
                    .ToArray(),

            Results =
                processResult.Steps
                    .Where(x =>
                        x.ExecutionStatus != StepExecutionStatus.Pending ||
                        x.RuntimeMessages.Count > 0 ||
                        x.BusinessMessages.Count > 0)
                    .Select(CreateStepResult)
                    .ToArray()
        };
    }

    public StepExecutionResponse CreateStepResponse(
        ProcessResult processResult,
        ProcessStepResult stepResult,
        IProcessStepRegistry registry,
        string serviceName)
    {
        return new StepExecutionResponse
        {
            ProcessId =
                processResult.ProcessId,

            StepName =
                stepResult.StepName,

            // RequiredStep is null when TargetProcessorName is set —
            // consumer must call the target processor's state endpoint instead.
            RequiredStep =
                processResult.TargetProcessorName is null
                    ? processResult.RequiredStep
                    : null,

            TargetProcessorName =
                processResult.TargetProcessorName,

            Outcome = stepResult.Outcome,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(stepName =>
                        {
                            var registration = registry.Find(stepName)
                                ?? throw new KaleidoFrameworkException(
                                    FrameworkErrorCodes.MissingRegistration,
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return responseFactory.CreateStepSummary(
                                ToSummary(registration),
                                serviceName);
                        })
                    .ToList(),

            Messages =
                ToMessages(stepResult)
                    .ToList()
        };
    }

    public StepExecutionResponse<TResponse> CreateStepResponse<TResponse>(
        ProcessResult processResult,
        ProcessStepResult stepResult,
        IProcessStepRegistry registry,
        string serviceName)
    {
        var response =
            CreateStepResponse(
                processResult,
                stepResult,
                registry,
                serviceName);

        return new StepExecutionResponse<TResponse>
        {
            ProcessId =
                response.ProcessId,

            StepName =
                response.StepName,

            RequiredStep =
                response.RequiredStep,

            TargetProcessorName =
                response.TargetProcessorName,

            Outcome =
                response.Outcome,

            AvailableSteps =
                response.AvailableSteps,

            Messages =
                response.Messages,

            Result =
                (TResponse?)stepResult.Response
        };
    }

    private static ProcessExecutionStepResponse CreateStepResult(
        ProcessStepResult stepResult)
    {
        return new ProcessExecutionStepResponse
        {
            StepName = stepResult.StepName,
            Response = stepResult.Response ?? new { },
            Messages = ToMessages(stepResult).ToArray()
        };
    }

    private static ProcessorStepSummary ToSummary(ProcessStepRegistration registration) =>
        new()
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled
        };

    private static IEnumerable<ProcessMessage> ToMessages(
        ProcessStepResult stepResult)
    {
        return stepResult.RuntimeMessages
            .Select(message =>
                new ProcessMessage
                {
                    Type = message.Type,
                    Message = message.Message,
                    Code = message.Code.ToString()
                })
            .Concat(
                stepResult.BusinessMessages
                    .Select(message =>
                        new ProcessMessage
                        {
                            Type = message.Type,
                            Message = message.Message,
                            Code = message.Code
                        }));
    }
}
