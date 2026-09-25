
namespace Kaleido.Queryable;

internal static class QueryViewTypeExtensions
{
    private static readonly Type[] ContextSourceDefinitions =
    [
        typeof(IQueryContextSource<>),
        typeof(IQueryContextSourceAsync<>)
    ];

    private static readonly Type[] SyncViewSourceDefinitions =
    [
        typeof(IQueryViewSource<,>),
        typeof(IQueryViewSource<,,>)
    ];

    private static readonly Type[] AsyncViewSourceDefinitions =
    [
        typeof(IQueryViewSourceAsync<,>),
        typeof(IQueryViewSourceAsync<,,>)
    ];

    private static readonly Type[] DelegateViewSourceDefinitions =
    [
        typeof(IDelegateQueryViewSource<,>),
        typeof(IDelegateQueryViewSource<,,>)
    ];

    /// <summary>
    /// Returns the <see cref="IQueryContextSource{T}"/> / <see cref="IQueryContextSourceAsync{T}"/>
    /// interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetContextSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(ContextSourceDefinitions);

    /// <summary>
    /// Returns the synchronous <see cref="IQueryViewSource{TQueryView,TView}"/>
    /// interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetSyncViewSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(SyncViewSourceDefinitions);

    /// <summary>
    /// Returns the asynchronous <see cref="IQueryViewSourceAsync{TQueryContext,TView}"/>
    /// interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetAsyncViewSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(AsyncViewSourceDefinitions);

    /// <summary>
    /// Returns all <see cref="IQueryViewSource{TQueryView,TView}"/> and
    /// <see cref="IQueryViewSourceAsync{TQueryContext,TView}"/> interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetViewSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(
            [.. SyncViewSourceDefinitions, .. AsyncViewSourceDefinitions]);

    /// <summary>
    /// Returns the <see cref="IDelegateQueryViewSource{TDelegateContext,TView}"/>
    /// interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetDelegateViewSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(DelegateViewSourceDefinitions);

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
    /// whose first generic argument equals <paramref name="firstArgumentType"/>
    /// (e.g. an <see cref="IQueryContextSource{T}"/> for a specific context type).
    /// </summary>
    internal static bool ImplementsGenericInterfaceFor(
        this Type type,
        Type firstArgumentType,
        params Type[] genericDefinitions) =>
        type.GetInterfaces()
            .Any(i =>
                i.IsGenericType &&
                genericDefinitions.Contains(i.GetGenericTypeDefinition()) &&
                i.GenericTypeArguments[0] == firstArgumentType);

    private static Type[] GetGenericInterfaces(
        this Type type,
        Type[] genericDefinitions) =>
        type.GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                genericDefinitions.Contains(i.GetGenericTypeDefinition()))
            .ToArray();
}
