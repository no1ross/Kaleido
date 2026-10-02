namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessorAvailableStepEntity
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

    public int Sequence
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