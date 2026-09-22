using System.Reflection;
using Kaleido.Exceptions;

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

    /// <summary>
    /// Evaluates the configured <see cref="KaleidoServiceOptions.TypeFilter"/> for a candidate type,
    /// converting a filter failure into a <see cref="KaleidoConfigurationException"/>.
    /// </summary>
    internal static bool PassesTypeFilter(
        this Type type,
        Func<Type, bool>? typeFilter,
        string errorCode,
        string typeLabel)
    {
        try
        {
            return typeFilter?.Invoke(type) ?? true;
        }
        catch (Exception exception)
        {
            throw new KaleidoConfigurationException(
                errorCode,
                $"The configured TypeFilter failed while evaluating {typeLabel} '{type.FullName ?? type.Name}'.",
                exception);
        }
    }
}
