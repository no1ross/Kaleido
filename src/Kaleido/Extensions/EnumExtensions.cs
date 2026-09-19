using System.ComponentModel;
using System.Reflection;

namespace Kaleido.Extensions;

public static class EnumExtensions
{
    public static string ToName(
        Enum value)
    {
        return value.ToString();
    }
}