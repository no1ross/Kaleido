using Kaleido;
using Kaleido.Process;
using Kaleido.Process.Registry;

namespace Kaleido.Http.Process.Contracts;

// Wire shapes (ProcessExecutionResponse, StepExecutionResponse, etc.)
// are defined in Kaleido.Http.Abstractions.
// Factory methods and mapping logic that depend on server-side types live here.

public static class ProcessExecutionResponseFactory
{
    public static ProcessExecutionResponse Create(
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
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return ProcessContractMapper.ToSummary(
                                ProcessRegistryProjection.ProjectSummary(registration),
                                serviceName);
                        })
                    .ToArray(),

            Results =
                processResult.Steps
                    .Where(x =>
                        x.ExecutionStatus != StepExecutionStatus.Pending ||
                        x.RuntimeMessages.Count > 0 ||
                        x.BusinessMessages.Count > 0)
                    .Select(x =>
                        ProcessExecutionStepResponseFactory.Create(x))
                    .ToArray()
        };
    }
}

public static class ProcessExecutionStepResponseFactory
{
    public static ProcessExecutionStepResponse Create(
        ProcessStepResult stepResult)
    {
        ArgumentNullException.ThrowIfNull(stepResult);

        return new ProcessExecutionStepResponse
        {
            StepName = stepResult.StepName,
            Response = stepResult.Response ?? new { },
            Messages = ProcessContractMapper.ToMessages(stepResult).ToArray()
        };
    }
}

public static class StepExecutionResponseFactory
{
    public static StepExecutionResponse Create(
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
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return ProcessContractMapper.ToSummary(
                                ProcessRegistryProjection.ProjectSummary(registration),
                                serviceName);
                        })
                    .ToList(),

            Messages =
                ProcessContractMapper.ToMessages(stepResult)
                    .ToList()
        };
    }

    public static StepExecutionResponse<TResponse> Create<TResponse>(
        ProcessResult processResult,
        ProcessStepResult stepResult,
        IProcessStepRegistry registry,
        string serviceName)
    {
        var response =
            Create(
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
}

internal static class ProcessContractMapper
{
    public static ProcessStepSummary ToSummary(
        ProcessorStepSummary registration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var stepName =
            registration.Name.ToLowerInvariant();

        return new ProcessStepSummary
        {
            Name = registration.Name,
            Version = registration.Version,
            DisplayName = registration.DisplayName,
            Description = registration.Description,
            Repeatable = registration.Repeatable,
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceName, stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(serviceName, stepName)
        };
    }

    public static IEnumerable<ProcessMessage> ToMessages(
        ProcessStepResult stepResult)
    {
        ArgumentNullException.ThrowIfNull(stepResult);

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
