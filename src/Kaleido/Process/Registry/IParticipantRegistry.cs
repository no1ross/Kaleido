namespace Kaleido.Process.Registry;

public interface IProcessorRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }
}
