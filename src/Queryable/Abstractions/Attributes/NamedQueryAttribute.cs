using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NamedQueryAttribute : Attribute
{
    public NamedQueryAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }
    public string Description { get; }
}
