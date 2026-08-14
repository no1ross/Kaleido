using Kaleido.Process.Participant;
using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ExecuteProcessRequest
{
    public Guid? ParticipantProcessId
    {
        get;
        init;
    }

    public required string RequestId
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepRequest> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessStepRequest
{
    public required string StepName
    {
        get;
        init;
    }

    public required JsonElement Request
    {
        get;
        init;
    }
}


public sealed record ExecuteStepRequest<TProcessStep>
{
    public Guid? ParticipantProcessId
    {
        get;
        init;
    }

    public required string RequestId
    {
        get;
        init;
    }

    public required TProcessStep ProcessStep
    {
        get;
        init;
    }

    public ProcessRequest ToProcessRequest(
        string stepName,
        string requestId)
    {
        return new ProcessRequest
        {
            ParticipantProcessId = ParticipantProcessId,

            RequestId =
                requestId,

            Participant =
                new ParticipantRequest
                {
                    Steps =
                        new Dictionary<string, object?>(
                            StringComparer.OrdinalIgnoreCase)
                        {
                            [stepName] = ProcessStep!
                        }
                }
        };
    }
}