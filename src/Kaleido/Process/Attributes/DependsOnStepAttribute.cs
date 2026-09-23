
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnStepAttribute(
    Type dependsOnStep) : Attribute
{
    public Type DependsOnStep { get; } = dependsOnStep;
}
