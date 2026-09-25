namespace Kaleido.Process;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ProcessStepAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RepeatableAttribute : Attribute
{
    public RepeatableAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnStepAttribute(
    Type dependsOnStep) : Attribute
{
    public Type DependsOnStep { get; } = dependsOnStep;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableAfterAttribute(
    Type availableAfterStep) : Attribute
{
    public Type AvailableAfterStep { get; } = availableAfterStep;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AvailableUntilAttribute(
    Type availableUntilStep) : Attribute
{
    public Type AvailableUntilStep { get; } = availableUntilStep;
}
