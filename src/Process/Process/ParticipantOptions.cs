namespace Kaleido.Process;

public class ProcessorOptions
{
    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, the registry will identify this processor as the one consumers
    /// should start with. Only one processor in a distributed system should have
    /// this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; set; }

    internal Func<Type, bool>? TypeFilter { get; set; }
}
