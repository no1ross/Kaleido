namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as sortable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SortableAttribute : Attribute
{
    public SortableAttribute()
    {

    }
}
