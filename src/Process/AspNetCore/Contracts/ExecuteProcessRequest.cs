using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ExecuteProcessRequest
{
    public string? ParticipantProcessId
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
    public string? ParticipantProcessId
    {
        get;
        init;
    }

    public required TProcessStep ProcessStep
    {
        get;
        init;
    }
}