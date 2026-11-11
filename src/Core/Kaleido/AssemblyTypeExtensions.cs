using System.Reflection;

namespace Kaleido;

internal static class AssemblyTypeExtensions
{
    /// <summary>
    /// Scans the registered assemblies for concrete classes visible to the framework.
    /// The predicate intentionally excludes private/protected nested types.
    /// </summary>
    internal static Type[] ScanTypes(
        this IEnumerable<Assembly> assemblies) =>
        assemblies
            .Distinct()
            .SelectMany(x => x.DefinedTypes)
            .Where(x =>
                x.IsClass &&
                !x.IsAbstract &&
                (
                    x.IsPublic ||
                    x.IsNestedPublic ||
                    x.IsNotPublic ||
                    x.IsNestedAssembly
                ))
            .Select(x => x.AsType())
            .ToArray();
}
