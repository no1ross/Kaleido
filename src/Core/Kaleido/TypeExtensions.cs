namespace Kaleido;

internal static class TypeExtensions
{
    /// <summary>
    /// Returns true if this type implements any of the given open generic interface definitions.
    /// </summary>
    internal static bool ImplementsGenericInterface(
        this Type type,
        params Type[] genericDefinitions) =>
        type.GetInterfaces()
            .Any(i =>
                i.IsGenericType &&
                genericDefinitions.Contains(i.GetGenericTypeDefinition()));

    /// <summary>
    /// Returns true if this type implements any of the given open generic interface definitions
    /// whose first generic argument equals <paramref name="firstArgumentType"/>.
    /// </summary>
    internal static bool ImplementsGenericInterfaceFor(
        this Type type,
        Type firstArgumentType,
        params Type[] genericDefinitions) =>
        type.GetGenericInterfacesFor(
                firstArgumentType,
                genericDefinitions)
            .Length > 0;

    /// <summary>
    /// Returns the closed generic interfaces implemented by this type whose definition is in
    /// <paramref name="genericDefinitions"/> and whose first generic argument equals
    /// <paramref name="firstArgumentType"/>.
    /// </summary>
    internal static Type[] GetGenericInterfacesFor(
        this Type type,
        Type firstArgumentType,
        params Type[] genericDefinitions) =>
        type.GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                genericDefinitions.Contains(i.GetGenericTypeDefinition()) &&
                i.GenericTypeArguments.Length > 0 &&
                i.GenericTypeArguments[0] == firstArgumentType)
            .ToArray();

    /// <summary>
    /// Returns the closed generic interfaces implemented by this type whose definition is in
    /// <paramref name="genericDefinitions"/>.
    /// </summary>
    internal static Type[] GetGenericInterfaces(
        this Type type,
        params Type[] genericDefinitions) =>
        type.GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                genericDefinitions.Contains(i.GetGenericTypeDefinition()))
            .ToArray();
}
