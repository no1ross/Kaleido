namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as filterable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FilterableAttribute(
    params FilterOperator[] operators) : Attribute
{
    public IReadOnlyList<FilterOperator> Operators { get; } = operators;
}
