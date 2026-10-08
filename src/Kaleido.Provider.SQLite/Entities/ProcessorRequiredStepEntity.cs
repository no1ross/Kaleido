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

    /// <summary>The pending information request for this step, as JSON; null when none.</summary>
    public string? InformationRequestJson
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
