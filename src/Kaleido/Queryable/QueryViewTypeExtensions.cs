
namespace Kaleido.Queryable;

/// <summary>
/// Reflection helpers for the Queryable identity interfaces. The shorter generic forms
/// (without a parameters type) derive from the full forms, so every implementation exposes a
/// full-form interface; these helpers only look at the full forms.
/// </summary>
internal static class QueryViewTypeExtensions
{
    /// <summary>
    /// The synchronous local source interfaces (<see cref="IQuerySource{TQueryContext}"/>)
    /// implemented by this type.
    /// </summary>
    internal static Type[] GetSyncSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(IQuerySource<>));

    /// <summary>
    /// The asynchronous local source interfaces (<see cref="IQuerySourceAsync{TQueryContext}"/>)
    /// implemented by this type.
    /// </summary>
    internal static Type[] GetAsyncSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(IQuerySourceAsync<>));

    /// <summary>
    /// The local source marker interfaces (<see cref="ILocalQuerySource{TQueryContext}"/>)
    /// implemented by this type.
    /// </summary>
    internal static Type[] GetLocalSourceMarkerInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(ILocalQuerySource<>));

    /// <summary>
    /// The delegated source interfaces
    /// (<see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>) implemented by this type.
    /// Generic arguments: query context, result, parameters.
    /// </summary>
    internal static Type[] GetDelegatedSourceInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(IDelegatedQuerySource<,,>));

    /// <summary>
    /// The synchronous view interfaces
    /// (<see cref="IQueryViewSource{TSource,TQueryContext,TView,TViewParameters}"/>) implemented by this type.
    /// Generic arguments: source, query context, view, parameters.
    /// </summary>
    internal static Type[] GetSyncViewInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(IQueryViewSource<,,,>));

    /// <summary>
    /// The asynchronous view interfaces
    /// (<see cref="IQueryViewSourceAsync{TSource,TQueryContext,TView,TViewParameters}"/>) implemented by this type.
    /// Generic arguments: source, query context, view, parameters.
    /// </summary>
    internal static Type[] GetAsyncViewInterfaces(
        this Type type) =>
        type.GetGenericInterfaces(typeof(IQueryViewSourceAsync<,,,>));

    /// <summary>All synchronous and asynchronous view interfaces implemented by this type.</summary>
    internal static Type[] GetViewInterfaces(
        this Type type) =>
        [.. type.GetSyncViewInterfaces(), .. type.GetAsyncViewInterfaces()];

    /// <summary>Whether this type is a local source (implements the local source marker).</summary>
    internal static bool IsLocalQuerySource(
        this Type type) =>
        type.GetLocalSourceMarkerInterfaces().Length > 0;

    /// <summary>Whether this type is a delegated source.</summary>
    internal static bool IsDelegatedQuerySource(
        this Type type) =>
        type.GetDelegatedSourceInterfaces().Length > 0;

    /// <summary>Whether this type is a local query view.</summary>
    internal static bool IsQueryView(
        this Type type) =>
        type.GetViewInterfaces().Length > 0;
}
