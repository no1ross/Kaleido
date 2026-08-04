using Kaleido.Eventing;
namespace Kaleido.Process.Eventing;

public interface IProcessEvent : IKaleidoEvent
{
    string ParticipantProcessId { get; }

    string RequestId { get; }

    DateTimeOffset OccurredOn { get; }
}
