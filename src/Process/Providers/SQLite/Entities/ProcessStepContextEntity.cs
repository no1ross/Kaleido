using Kaleido.Process.Execution;

namespace Kaleido.Process.Providers.SQLite.Entities;

public sealed class ProcessStepContextEntity
{
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

    public string Version
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

    public DateTimeOffset? LastExecuted
    {
        get;
        set;
    }

    public ProcessContextEntity Context
    {
        get;
        set;
    } = null!;
}