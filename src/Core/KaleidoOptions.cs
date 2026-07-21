using System.Reflection;

namespace Kaleido;

public sealed class KaleidoOptions
{
    internal HashSet<Assembly> Assemblies { get; } = [];

    public bool ValidateRegistrations { get; set; } = true;

    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Assemblies.Add(assembly);
    }

    public void RegisterFromAssemblyContaining<T>()
    {
        Assemblies.Add(typeof(T).Assembly);
    }
}
