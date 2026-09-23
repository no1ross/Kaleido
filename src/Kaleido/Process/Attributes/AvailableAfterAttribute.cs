
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableAfterAttribute(
    Type availableAfterStep) : Attribute
{
    public Type AvailableAfterStep { get; } = availableAfterStep;
}
