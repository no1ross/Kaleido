namespace Kaleido.Process.Registry;

public interface IProcessorRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }

    ProcessorRegistryItem? Find(string name);

    ProcessorRegistryItem GetRegistration(string name);
}
