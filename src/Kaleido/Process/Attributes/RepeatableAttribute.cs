
namespace Kaleido.Process.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RepeatableAttribute : Attribute
{
    public RepeatableAttribute()
    {
    }
}