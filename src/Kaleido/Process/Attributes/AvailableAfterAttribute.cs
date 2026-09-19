
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableAfterAttribute : Attribute
{
    public AvailableAfterAttribute(Type availableAfterStep)
    {
        AvailableAfterStep = availableAfterStep;
    }

    public Type AvailableAfterStep { get; }
}
