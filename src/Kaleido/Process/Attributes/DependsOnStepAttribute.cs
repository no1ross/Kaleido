
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
