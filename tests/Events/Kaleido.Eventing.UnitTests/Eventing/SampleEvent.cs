namespace Kaleido.Eventing.UnitTests;

internal sealed record SampleEvent : IKaleidoEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; init; }
    public string Name { get; init; } = "sample";
    public string? StepName { get; init; }
    public object? Request { get; init; }
    public string Status { get; init; } = "completed";
    public string? QuerySourceName { get; init; }
    public int TotalCount { get; init; }
}
