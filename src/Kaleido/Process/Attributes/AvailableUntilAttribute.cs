
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableUntilAttribute : Attribute
{
    public AvailableUntilAttribute(Type availableUntilStep)
    {
        AvailableUntilStep = availableUntilStep;
    }

    public Type AvailableUntilStep { get; }
}
