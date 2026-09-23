
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableUntilAttribute(
    Type availableUntilStep) : Attribute
{
    public Type AvailableUntilStep { get; } = availableUntilStep;
}
