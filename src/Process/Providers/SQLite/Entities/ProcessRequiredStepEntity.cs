namespace Kaleido.Process.Providers.SQLite.Entities;

public sealed class ProcessRequiredStepEntity
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

    public ProcessContextEntity Context
    {
        get;
        set;
    } = null!;
}
