
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
        typeof(IDelegatedQueryViewSource<,>),
        typeof(IDelegatedQueryViewSource<,,>)
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
    /// Returns the <see cref="IDelegatedQueryViewSource{TDelegateContext,TView}"/>
    /// interfaces implemented by this type.
    /// </summary>
    internal static Type[] GetDelegateViewSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(DelegateViewSourceDefinitions);

    /// <summary>
    /// Returns the <see cref="IQueryViewSource{TQueryView,TView}"/> /
    /// <see cref="IQueryViewSourceAsync{TQueryContext,TView}"/> interface implemented by
    /// this query view type (preferring the three-parameter overload when both exist).
    /// </summary>
    internal static Type GetQueryViewInterface(
        this Type queryViewType) =>
        queryViewType
            .GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                (
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,,>)
                ))
            .OrderByDescending(
                i => i.GenericTypeArguments.Length)
            .First();
}
