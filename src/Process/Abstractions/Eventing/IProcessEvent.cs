using Kaleido.Eventing;
namespace Kaleido.Process.Eventing;

public interface IProcessEvent : IKaleidoEvent
{
    Guid ParticipantProcessId { get; }

    string RequestId { get; }

    DateTimeOffset OccurredOn { get; }
}
