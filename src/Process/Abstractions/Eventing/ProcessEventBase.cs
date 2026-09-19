namespace Kaleido.Process.Eventing;

public abstract record ProcessEventBase : IProcessEvent
{
    public required DateTimeOffset OccurredOn { get; init; }
}
