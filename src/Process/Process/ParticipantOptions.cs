namespace Kaleido.Process;

public class ProcessorOptions : KaleidoOptions
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, the registry will identify this processor as the one consumers
    /// should start with. Only one processor in a distributed system should have
    /// this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; set; }

    /// <summary>
    /// Uniquely identifies this running instance of the processor.
    /// Defaults to a new GUID generated at registration time.
    /// </summary>
    public Guid InstanceId { get; set; } = Guid.NewGuid();

    internal Func<Type, bool>? TypeFilter { get; set; }
}
