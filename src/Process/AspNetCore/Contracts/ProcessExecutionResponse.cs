using Kaleido.Process.Participant;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionResponse
{
    public required string ParticipantProcessId
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
}


public sealed record ProcessExecutionResponse<TResponse>
{
    public required string ParticipantProcessId
    {
        get;
        init;
    }

    public required string StepName
    {
        get;
        init;
    }

    public required TResponse Result
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

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];

    public static ProcessExecutionResponse<TResponse> Create(
        ParticipantProcessResult processResult,
        ParticipantStepResult stepResult,
        IProcessStepRegistry registry)
    {
        return new()
        {
            ParticipantProcessId =
                processResult.ParticipantProcessId
                    .ToString(),

            StepName =
                stepResult.StepName,

            Result =
                (TResponse)stepResult.Response,

            RequiredStep =
                processResult.RequiredStep,

            AvailableSteps =
                processResult.AvailableSteps
                    .Select(stepName =>
                    {
                        var registration =
                            registry.GetRegistration(
                                stepName);

                        return new ProcessStepSummary
                        {
                            Name =
                                registration.Metadata.Name,

                            Version =
                                registration.Metadata.Version,

                            DisplayName =
                                registration.Metadata.DisplayName,

                            Description =
                                registration.Metadata.Description,

                            Repeatable =
                                registration.Repeatable.Enabled,

                            ExecuteUrl =
                                $"/processes/steps/{registration.Metadata.Name}",

                            MetadataUrl =
                                $"/processes/steps/{registration.Metadata.Name}/metadata"
                        };
                    })
                    .ToList(),

            Messages =
                stepResult.Messages
                    .Select(message =>
                        new ProcessMessage
                        {
                            Severity = message.Type,
                            Message = message.Message,
                            Code = message.Code
                        })
                    .ToList()
        };
    }
}


public sealed record ProcessMessage
{
    public required MessageType Severity
    {
        get;
        init;
    }

    public required string Message
    {
        get;
        init;
    }

    public required StepProcessingMessageCode Code
    {
        get;
        init;
    }
}
