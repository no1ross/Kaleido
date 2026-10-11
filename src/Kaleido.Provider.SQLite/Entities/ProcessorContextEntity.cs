namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessorContextEntity
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

    /// <summary>The caller that created the process (a tag only, never enforced); null when created anonymously.</summary>
    public string? Owner
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

    public ICollection<ProcessorStepContextEntity> Steps
    {
        get;
        set;
    } = new List<ProcessorStepContextEntity>();

    public ICollection<ProcessorAvailableStepEntity> AvailableSteps
    {
        get;
        set;
    } = new List<ProcessorAvailableStepEntity>();

    /// <summary>
    /// Zero or one rows — present when the process is awaiting a specific required step.
    /// </summary>
    public ProcessorRequiredStepEntity? RequiredStep
    {
        get;
        set;
    }
}
