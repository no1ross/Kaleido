using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ExecuteProcessRequest
{
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
    public required TProcessStep ProcessStep
    {
        get;
        init;
    }

    public ProcessRequest ToProcessRequest(
        string stepName,
        Guid? processId = null)
    {
        return new ProcessRequest
        {
            ProcessId = processId,

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
