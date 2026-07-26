namespace Kaleido.Queryable.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class NamedQueryParameterAttribute : Attribute
{
    public NamedQueryParameterAttribute(string name, Type parameterType, bool required = false)
    {
        Name = name;
        ParameterType = parameterType;
        Required = required;
    }

    public string Name { get; }

    public Type ParameterType { get; }

    public bool Required { get; set; }

    public string? Description { get; set; }

    public object? DefaultValue { get; set; }
}
