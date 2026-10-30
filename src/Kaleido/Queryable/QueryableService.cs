using System.Reflection;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable;

/// <summary>In-process entry point for executing Queryable requests.</summary>
internal interface IQueryableService
{
    /// <summary>Executes a query against a query source or a query view.</summary>
    /// <typeparam name="TQuery">
    /// What to query: a local query view type (a view query), or a query source type (a direct
    /// query of a local source, or a delegated source).
    /// </typeparam>
    /// <typeparam name="TResult">
    /// The returned record type: the view's record for a view, the query context for a local
    /// source, or the source's result record for a delegated source.
    /// </typeparam>
    /// <param name="request">The query and parameters.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The query result.</returns>
    /// <exception cref="KaleidoFrameworkException">
    /// <typeparamref name="TQuery"/> is not a registered source or view, or
    /// <typeparamref name="TResult"/> does not match what it returns.
    /// </exception>
    Task<QueryResult<TResult>> QueryAsync<TQuery, TResult>(IQueryRequest request, CancellationToken cancellationToken)
        where TQuery : class
        where TResult : class;
}

internal sealed class QueryableService(
    IServiceScopeFactory scopeFactory,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IKaleidoInboundCallerAccessor inboundCaller,
    IQueryViewRegistry viewRegistry,
    IQuerySourceRegistry sourceRegistry)
    : IQueryableService
{
    private static readonly MethodInfo ExecuteViewTypedAsyncMethod =
        GetMethod(nameof(ExecuteViewTypedAsync));

    private static readonly MethodInfo ExecuteDirectTypedAsyncMethod =
        GetMethod(nameof(ExecuteDirectTypedAsync));

    private static readonly MethodInfo ExecuteDelegatedTypedAsyncMethod =
        GetMethod(nameof(ExecuteDelegatedTypedAsync));

    // Closed-generic MethodInfo cache — MakeGenericMethod allocates per call,
    // and these execute once per query request.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MethodInfo Open, Type A, Type B), MethodInfo> ClosedMethods =
        new();

    public async Task<QueryResult<TResult>> QueryAsync<TQuery, TResult>(
        IQueryRequest request,
        CancellationToken cancellationToken)
        where TQuery : class
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(request);

        // Disposed asynchronously: a scoped dependency may implement only IAsyncDisposable (#523).
        await using var scope =
            scopeFactory.CreateAsyncScope();

        // The query runs in its own scope, whose scoped correlation would otherwise be empty (#413):
        // carry the request's correlation in, as the Process runtime does for a step handler, so
        // sources and views see the caller and their outbound calls forward the request's ids.
        scope.ServiceProvider
            .GetRequiredService<IKaleidoCorrelationContextInitializer>()
            .Initialize(correlationAccessor.Current);

        // And the request's caller, so a source's calls are made for that caller (ADR 0022, #428);
        // outside a request there is none, and its calls stay the service's own.
        if (inboundCaller.IsInboundRequest)
        {
            scope.ServiceProvider
                .GetRequiredService<IKaleidoInboundCallerAccessor>()
                .Set(inboundCaller.Principal, inboundCaller.Credential);
        }

        // A type is exactly one capability (enforced at startup), so a view lookup and a
        // source lookup never both match.
        if (viewRegistry.Find(typeof(TQuery)) is { } viewRegistration)
        {
            if (viewRegistration.ViewType != typeof(TResult))
            {
                throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Query view '{viewRegistration.QueryViewType.FullName}' returns " +
                    $"'{viewRegistration.ViewType.FullName}', but query requested '{typeof(TResult).FullName}'.");
            }

            return await InvokeAsync<TResult>(
                ExecuteViewTypedAsyncMethod,
                viewRegistration.QueryContextType,
                [scope.ServiceProvider, request, sourceRegistry.GetRegistration(viewRegistration.SourceType), viewRegistration, cancellationToken]);
        }

        var source =
            sourceRegistry.GetRegistration(
                typeof(TQuery));

        if (source.ResultType != typeof(TResult))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query source '{source.SourceType.FullName}' returns " +
                $"'{source.ResultType.FullName}', but query requested '{typeof(TResult).FullName}'.");
        }

        return await InvokeAsync<TResult>(
            source.Metadata.Kind == QuerySourceKind.Delegated
                ? ExecuteDelegatedTypedAsyncMethod
                : ExecuteDirectTypedAsyncMethod,
            source.QueryContextType,
            [scope.ServiceProvider, request, source, cancellationToken]);
    }

    private static async Task<QueryResult<TResult>> InvokeAsync<TResult>(
        MethodInfo open,
        Type contextType,
        object?[] arguments)
        where TResult : class
    {
        var closed =
            ClosedMethods.GetOrAdd(
                (open, contextType, typeof(TResult)),
                key => key.Open.MakeGenericMethod(key.A, key.B));

        if (closed.Invoke(null, arguments) is not Task<QueryResult<TResult>> typedTask)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query execution '{open.Name}' did not return '{typeof(QueryResult<TResult>).FullName}'.");
        }

        return await typedTask;
    }

    private static Task<QueryResult<TResult>> ExecuteViewTypedAsync<TQueryContext, TResult>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QuerySourceRegistration source,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TQueryContext : class, IQueryContext
        where TResult : class =>
        serviceProvider
            .GetRequiredService<IQueryContextEngine<TQueryContext, TResult>>()
            .ExecuteAsync(request, source, viewRegistration, cancellationToken);

    private static Task<QueryResult<TResult>> ExecuteDirectTypedAsync<TQueryContext, TResult>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken)
        where TQueryContext : class, IQueryContext
        where TResult : class =>
        serviceProvider
            .GetRequiredService<IQueryContextEngine<TQueryContext, TResult>>()
            .ExecuteAsync(request, source, cancellationToken);

    private static Task<QueryResult<TResult>> ExecuteDelegatedTypedAsync<TQueryContext, TResult>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken)
        where TQueryContext : class, IQueryContext
        where TResult : class =>
        serviceProvider
            .GetRequiredService<IDelegatedQuerySourceEngine<TQueryContext, TResult>>()
            .ExecuteAsync(request, source, cancellationToken);

    private static MethodInfo GetMethod(string name) =>
        typeof(QueryableService)
            .GetMethod(
                name,
                BindingFlags.Static |
                BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{name}'.");
}
