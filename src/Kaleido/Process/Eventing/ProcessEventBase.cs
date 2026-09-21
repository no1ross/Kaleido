namespace Kaleido.Process.Eventing;

public interface IProcessEvent : IKaleidoEvent
{
}

public abstract record ProcessEventBase : IProcessEvent
{
    public required DateTimeOffset OccurredOn { get; init; }
}
