using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ExecuteProcessContract
{
    public string? ParticipantProcessId
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepRequestContract> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessStepRequestContract
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


public sealed record ExecuteStepContract<TProcessStep>
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