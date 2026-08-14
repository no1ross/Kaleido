using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.Providers.SQLite.Entities;

public sealed class ProcessContextEntity
{
    public Guid ParticipantProcessId
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

    public string? RequiredStep
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
}