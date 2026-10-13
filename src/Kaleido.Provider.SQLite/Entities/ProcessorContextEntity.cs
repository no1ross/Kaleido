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

    // Optimistic concurrency token: incremented by every save.
    public long Version
    {
        get;
        set;
    }

    /// <summary>The caller that created the process (a tag only, never enforced); null when created anonymously.</summary>
    // Created-by / updated-by tags (KaleidoCaller).
    public string? CreatedByName
    {
        get;
        set;
    }

    public string? CreatedById
    {
        get;
        set;
    }

    public string? UpdatedByName
    {
        get;
        set;
    }

    public string? UpdatedById
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
    } = [];

    public ICollection<ProcessorAvailableStepEntity> AvailableSteps
    {
        get;
        set;
    } = [];

    /// <summary>
    /// Zero or one rows — present when the process is awaiting a specific required step.
    /// </summary>
    public ProcessorRequiredStepEntity? RequiredStep
    {
        get;
        set;
    }
}
