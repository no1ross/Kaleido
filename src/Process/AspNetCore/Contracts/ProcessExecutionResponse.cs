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

public record StepExecutionResponse
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
        IProcessStepRegistry registry)
    {
        return new()
        {
            ParticipantProcessId =
                processResult.ParticipantProcessId
                    .ToString(),

            StepName =
                stepResult.StepName,

            RequiredStep =
                processResult.RequiredStep,

            Outcome = stepResult.Outcome,

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
                stepResult.RuntimeMessages
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
                                }))
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
        IProcessStepRegistry registry)
    {
        var response =
            StepExecutionResponse.Create(
                processResult,
                stepResult,
                registry);

        return new()
        {
            ParticipantProcessId =
                response.ParticipantProcessId,

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
