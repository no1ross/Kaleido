using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ExecuteProcessRequest
{
    public Guid? ProcessId
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
    public Guid? ProcessId
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
        string stepName)
    {
        return new ProcessRequest
        {
            ProcessId = ProcessId,

            Processor =
                new ProcessorRequest
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
