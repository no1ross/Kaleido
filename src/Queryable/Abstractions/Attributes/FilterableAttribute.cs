using Kaleido.Queryable;

namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as filterable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FilterableAttribute : Attribute
{
    /// <summary>Creates filter metadata for a property.</summary>
    /// <param name="operators">Operators supported by this property.</param>
    public FilterableAttribute(params FilterOperator[] operators) => Operators = operators;
    public IReadOnlyList<FilterOperator> Operators { get; }
}
