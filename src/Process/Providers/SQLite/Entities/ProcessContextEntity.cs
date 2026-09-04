using Kaleido.Process.Execution;

namespace Kaleido.Process.Providers.SQLite.Entities;

public sealed class ProcessContextEntity
{
    public Guid ProcessId
    {
        get;
        set;
    }

    public string? LatestRequestId
    {
        get;
        set;
    }

    public ProcessExecutionState State
    {
        get;
        set;
    }

    public DateTimeOffset CreatedUtc
    {
        get;
        set;
    }

    public DateTimeOffset UpdatedUtc
    {
        get;
        set;
    }

    public ICollection<ProcessStepContextEntity> Steps
    {
        get;
        set;
    } = new List<ProcessStepContextEntity>();

    public ICollection<ProcessAvailableStepEntity> AvailableSteps
    {
        get;
        set;
    } = new List<ProcessAvailableStepEntity>();

    /// <summary>
    /// Zero or one rows — present when the process is awaiting a specific required step.
    /// </summary>
    public ProcessRequiredStepEntity? RequiredStep
    {
        get;
        set;
    }
}