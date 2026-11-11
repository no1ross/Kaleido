namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessorStepContextEntity
{
    // The owning processor and its version (#552): with ProcessId, the key. An unversioned processor's
    // version is stored empty (key columns are never null).
    public string ProcessorName
    {
        get;
        set;
    } = string.Empty;

    public string ProcessorVersion
    {
        get;
        set;
    } = string.Empty;

    public Guid ProcessId
    {
        get;
        set;
    }

    public string StepName
    {
        get;
        set;
    } = string.Empty;

    public StepExecutionStatus Status
    {
        get;
        set;
    }

    public string? LatestRequestId
    {
        get;
        set;
    }

    public DateTimeOffset? FirstExecuted
    {
        get;
        set;
    }

    // Started-by / last-updated-by tags (KaleidoCaller).
    public string? StartedByName
    {
        get;
        set;
    }

    public string? StartedById
    {
        get;
        set;
    }

    public DateTimeOffset? LastExecuted
    {
        get;
        set;
    }

    public string? LastUpdatedByName
    {
        get;
        set;
    }

    public string? LastUpdatedById
    {
        get;
        set;
    }

    public ProcessorContextEntity? Context
    {
        get;
        set;
    }
}
