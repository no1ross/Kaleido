namespace Kaleido.Provider.SQLite.Entities;

internal sealed class ProcessorRequiredStepEntity
{
    public Guid ProcessId
    {
        get;
        set;
    }

    public string ProcessorName
    {
        get;
        set;
    } = string.Empty;

    public string StepName
    {
        get;
        set;
    } = string.Empty;

    public ProcessorContextEntity? Context
    {
        get;
        set;
    }
}
