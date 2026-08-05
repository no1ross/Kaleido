
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnStepAttribute : Attribute
{
    public DependsOnStepAttribute(Type dependsOnStep) 
    {
        DependsOnStep = dependsOnStep;
    }

    public Type DependsOnStep { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableAfterAttribute : Attribute
{
    public AvailableAfterAttribute(Type availableAfterStep)
    {
        AvailableAfterStep = availableAfterStep;
    }

    public Type AvailableAfterStep { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableUntilAttribute : Attribute
{
    public AvailableUntilAttribute(Type availableUntilStep)
    {
        AvailableUntilStep = availableUntilStep;
    }

    public Type AvailableUntilStep { get; }
}