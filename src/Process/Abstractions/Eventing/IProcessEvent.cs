using Kaleido.Eventing;

namespace Kaleido.Process.Eventing;

public interface IProcessEvent : IKaleidoEvent
{
    Guid ProcessId { get; }
}
