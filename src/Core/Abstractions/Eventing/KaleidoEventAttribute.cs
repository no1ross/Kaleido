namespace Kaleido.Eventing;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KaleidoEventAttribute : Attribute
{
    public required string Type { get; init; }
}
