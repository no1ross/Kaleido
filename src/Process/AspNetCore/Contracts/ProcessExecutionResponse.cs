using Kaleido.Process.Execution;
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

            RequiredStep =
                processResult.RequiredStep is null
                    ? null
                    : ProcessContractMapper.ToStepInfo(
                        processResult.RequiredStep,
                        registry,
                        options),

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(reference =>
                        ProcessContractMapper.ToStepInfo(
                            reference,
                            registry,
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

            RequiredStep =
                processResult.RequiredStep is null
                    ? null
                    : ProcessContractMapper.ToStepInfo(
                        processResult.RequiredStep,
                        registry,
                        options),

            Outcome = stepResult.Outcome,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(reference =>
                        ProcessContractMapper.ToStepInfo(
                            reference,
                            registry,
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
    /// <summary>
    /// Converts a <see cref="ProcessStepReference"/> from the runtime into a
    /// <see cref="ProcessStepInfo"/> for the HTTP response.
    ///
    /// Local steps (same processor) are resolved from the registry and get URLs.
    /// External steps get blank URLs — the consumer resolves them via its own registry.
    /// </summary>
    public static ProcessStepInfo ToStepInfo(
        ProcessStepReference reference,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);

        var localRegistration =
            registry.Find(reference.StepName);

        if (localRegistration is not null)
        {
            var stepName =
                localRegistration.Metadata.Name.ToLowerInvariant();

            return new ProcessStepInfo
            {
                ProcessorName = reference.ProcessorName,
                StepName = localRegistration.Metadata.Name,
                ExecuteUrl = ProcessContractUrls.ExecuteStep(options, stepName),
                MetadataUrl = ProcessContractUrls.StepMetadata(options, stepName)
            };
        }

        // External processor — no URLs available locally.
        return new ProcessStepInfo
        {
            ProcessorName = reference.ProcessorName,
            StepName = reference.StepName
        };
    }

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
