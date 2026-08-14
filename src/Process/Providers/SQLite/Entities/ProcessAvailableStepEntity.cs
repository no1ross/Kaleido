namespace Kaleido.Process.Providers.SQLite.Entities;

public sealed class ProcessAvailableStepEntity
{
    public Guid ParticipantProcessId
    {
        get;
        set;
    }

    public string StepName
    {
        get;
        set;
    } = string.Empty;

    public int Sequence
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