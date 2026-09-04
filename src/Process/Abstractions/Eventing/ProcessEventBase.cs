namespace Kaleido.Process.Eventing;

public abstract record ProcessEventBase : IProcessEvent
{
    public required Guid ProcessId { get; init; }

    public required string ProcessorName { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }
}
