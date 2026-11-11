namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessorAvailableStepEntity
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
