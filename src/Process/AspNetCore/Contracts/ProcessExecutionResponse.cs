using Kaleido.Process.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

// Wire shapes (ProcessExecutionResponse, StepExecutionResponse, etc.)
// are defined in Kaleido.Process.AspNetCore.Abstractions.
// Factory methods and mapping logic that depend on server-side types live here.

public static class ProcessExecutionResponseFactory
{
    public static ProcessExecutionResponse Create(
        ProcessorProcessResult processResult,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);

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
                        ProcessContractMapper.ToSummary(
                            registry.Find(stepName)
                                ?? throw new InvalidOperationException(
                                    $"Available step '{stepName}' was not found in the local registry."),
                            options))
                    .ToArray(),

            Results =
                processResult.Steps
                    .Where(x =>
                        x.ExecutionStatus is not null ||
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
        ProcessorStepResult stepResult)
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
        ProcessorProcessResult processResult,
        ProcessorStepResult stepResult,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

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
                        ProcessContractMapper.ToSummary(
                            registry.Find(stepName)
                                ?? throw new InvalidOperationException(
                                    $"Available step '{stepName}' was not found in the local registry."),
                            options))
                    .ToList(),

            Messages =
                ProcessContractMapper.ToMessages(stepResult)
                    .ToList()
        };
    }

    public static StepExecutionResponse<TResponse> Create<TResponse>(
        ProcessorProcessResult processResult,
        ProcessorStepResult stepResult,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        var response =
            Create(
                processResult,
                stepResult,
                registry,
                options);

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
        ProcessStepRegistration registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Metadata.Name.ToLowerInvariant();

        return new ProcessStepSummary
        {
            Name = registration.Metadata.Name,
            Version = registration.Metadata.Version,
            DisplayName = registration.Metadata.DisplayName,
            Description = registration.Metadata.Description,
            Repeatable = registration.Repeatable.Enabled,
            ExecuteUrl = ProcessContractUrls.ExecuteStep(
                options,
                stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(
                options,
                stepName)
        };
    }

    public static IEnumerable<ProcessMessage> ToMessages(
        ProcessorStepResult stepResult)
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
