namespace Kaleido.Queryable.Attributes;

/// <summary>Declares paging support for a record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PageableAttribute : Attribute
{
    public int DefaultSize { get; init; }
    public int MaxSize { get; init; }
}
