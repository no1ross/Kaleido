using Kaleido.Eventing;

namespace Kaleido.Queryable.Eventing;

public interface IQueryEvent : IKaleidoEvent
{
    Guid? ProcessId { get; }
}
