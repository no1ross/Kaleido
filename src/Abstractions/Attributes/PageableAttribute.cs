namespace Kaleido.Attributes;

/// <summary>Declares paging support for a record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PageableAttribute : Attribute
{
    /// <summary>Creates paging metadata.</summary>
    /// <param name="defaultSize">Default page size when the request does not specify one.</param>
    /// <param name="maxSize">Maximum allowed page size.</param>
    public PageableAttribute(int defaultSize, int maxSize)
    {
        DefaultSize = defaultSize;
        MaxSize = maxSize;
    }

    public int DefaultSize { get; }
    public int MaxSize { get; }
}
