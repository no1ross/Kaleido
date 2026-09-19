namespace Kaleido.Eventing;

/// <summary>
/// Marks a class as a Kaleido domain event and assigns it a stable string type discriminator
/// used for serialization and routing. Apply once per event class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KaleidoEventAttribute : Attribute
{
    /// <summary>
    /// The stable string type identifier for this event (e.g. <c>"process.step.completed"</c>).
    /// Must be unique within the system.
    /// </summary>
    public required string Type { get; init; }
}
