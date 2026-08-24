using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessExecutionStepResponse> Results
    {
        get;
        init;
    }
        = [];

    public static ProcessExecutionResponse Create(
        ParticipantProcessResult processResult,
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
                processResult.RequiredStep,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(stepName =>
                        ProcessContractMapper.ToSummary(
                            registry.GetRegistration(stepName),
                            options))
                    .ToArray(),

            Results =
                processResult.Steps
                    .Where(x =>
                        x.ExecutionStatus is not null ||
                        x.RuntimeMessages.Count > 0 ||
                        x.BusinessMessages.Count > 0)
                    .Select(x =>
                        ProcessExecutionStepResponse.Create(x))
                    .ToArray()
        };
    }
}

public sealed record ProcessExecutionStepResponse
{
    public required string StepName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];

    public required object Response
    {
        get;
        init;
    }

    public static ProcessExecutionStepResponse Create(
        ParticipantStepResult stepResult)
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

public record StepExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public required string StepName
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public StepExecutionOutcome? Outcome
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];

    public static StepExecutionResponse Create(
        ParticipantProcessResult processResult,
        ParticipantStepResult stepResult,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new()
        {
            ProcessId =
                processResult.ProcessId,

            StepName =
                stepResult.StepName,

            RequiredStep =
                processResult.RequiredStep,

            Outcome = stepResult.Outcome,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(stepName =>
                        ProcessContractMapper.ToSummary(
                            registry.GetRegistration(stepName),
                            options))
                    .ToList(),

            Messages =
                ProcessContractMapper.ToMessages(stepResult)
                    .ToList()
                    };
    }
}

public sealed record StepExecutionResponse<TResponse> : StepExecutionResponse
{
    public TResponse? Result
    {
        get;
        init;
    }

    new public static StepExecutionResponse<TResponse> Create(
        ParticipantProcessResult processResult,
        ParticipantStepResult stepResult,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        var response =
            StepExecutionResponse.Create(
                processResult,
                stepResult,
                registry,
                options);

        return new()
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
        ParticipantStepResult stepResult)
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

//public sealed record ProcessMessage
//{
//    public required MessageType Severity
//    {
//        get;
//        init;
//    }

//    public required string Message
//    {
//        get;
//        init;
//    }

//    public required StepProcessingMessageCode Code
//    {
//        get;
//        init;
//    }
//}
