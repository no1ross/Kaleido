namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessContextEntity
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

    /// <summary>Owner name captured at process creation; null when unowned.</summary>
    public string? Owner
    {
        get;
        set;
    }

    /// <summary>Owner roles, comma-delimited.</summary>
    public string OwnerRoles
    {
        get;
        set;
    } = string.Empty;

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