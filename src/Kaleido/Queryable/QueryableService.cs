using System.Reflection;
using Kaleido.Json;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable;

public interface IQueryableService
{
    Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(IQueryRequest request, CancellationToken cancellationToken = default)
        where TQueryView : class
        where TView : class;
}

internal sealed class QueryableService(
    IServiceScopeFactory scopeFactory,
    IValueConverter valueConverter,
    IDelegatedQueryViewRegistry delegatedViewRegistry,
    IQueryViewRegistry viewRegistry,
    IQueryContextRegistry contextRegistry)
    : IQueryableService
{
    private static readonly MethodInfo ExecuteTypedAsyncMethod =
        typeof(QueryableService)
            .GetMethod(
                nameof(ExecuteTypedAsync),
                BindingFlags.Static |
                BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(ExecuteTypedAsync)}'.");

    private static readonly MethodInfo ExecuteDelegatedTypedAsyncMethod =
        typeof(QueryableService)
            .GetMethod(
                nameof(ExecuteDelegatedTypedAsync),
                BindingFlags.Static |
                BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(ExecuteDelegatedTypedAsync)}'.");

    private static readonly MethodInfo ExecuteDirectTypedAsyncMethod =
        typeof(QueryableService)
            .GetMethod(
                nameof(ExecuteDirectTypedAsync),
                BindingFlags.Static |
                BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(ExecuteDirectTypedAsync)}'.");

    // Closed-generic MethodInfo cache — MakeGenericMethod allocates per call,
    // and these execute once per query request.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MethodInfo Open, Type A, Type B), MethodInfo> ClosedMethods =
        new();

    private static MethodInfo Close(MethodInfo open, Type a, Type b) =>
        ClosedMethods.GetOrAdd(
            (open, a, b),
            key => key.Open.MakeGenericMethod(key.A, key.B));

    public async Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(
        IQueryRequest request,
        CancellationToken cancellationToken = default)
        where TQueryView : class
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var delegatedViewRegistration =
            delegatedViewRegistry.Find(
                typeof(TQueryView));

        var viewRegistration =
            viewRegistry.Find(
                typeof(TQueryView));

        using var scope =
            scopeFactory.CreateScope();

        if (delegatedViewRegistration is not null)
        {
            ValidateDelegatedViewRegistration<TQueryView, TView>(
                delegatedViewRegistration);

            return await ExecuteDelegatedViewAsync<TView>(
                scope.ServiceProvider,
                NormalizeRequest(request, valueConverter, delegatedViewRegistration.QueryMetadata),
                delegatedViewRegistration,
                cancellationToken);
        }

        if (viewRegistration is not null)
        {
            ValidateViewRegistration<TQueryView, TView>(
                viewRegistration);

            var contextRegistration =
                contextRegistry.GetRegistration(
                    viewRegistration.QueryContextType);

            return await ExecuteWithDiscoveredContextAsync<TView>(
                scope.ServiceProvider,
                NormalizeRequest(request, valueConverter, contextRegistration.Metadata),
                contextRegistration,
                viewRegistration,
                cancellationToken);
        }

        var directContextRegistration =
            contextRegistry.GetRegistration(
                typeof(TQueryView));

        ValidateDirectQuery<TQueryView, TView>(
            directContextRegistration);

        return await ExecuteDirectWithDiscoveredContextAsync<TView>(
            scope.ServiceProvider,
            NormalizeRequest(request, valueConverter, directContextRegistration.Metadata),
            directContextRegistration,
            cancellationToken);
    }

    private static IQueryRequest NormalizeRequest(
        IQueryRequest request,
        IValueConverter valueConverter,
        QueryContextMetadata metadata) =>
        request.Query is null
            ? request
            : new NormalizedQueryRequest(
                request,
                request.Query.Normalize(valueConverter, metadata));

    private sealed record NormalizedQueryRequest(
        IQueryRequest Inner,
        QueryBody? Query)
        : IQueryRequest
    {
        public object? ViewParameters =>
            Inner.ViewParameters;

        public Type ViewParametersType =>
            Inner.ViewParametersType;
    }

    private async Task<QueryResult<TView>> ExecuteWithDiscoveredContextAsync<TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QueryContextRegistration contextRegistration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TView : class
    {
        var typedMethod =
            Close(
                ExecuteTypedAsyncMethod,
                viewRegistration.QueryContextType,
                typeof(TView));

        var result =
            typedMethod.Invoke(
                this,
                [serviceProvider, request, contextRegistration, viewRegistration, cancellationToken]);

        if (result is not Task<QueryResult<TView>> typedTask)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query execution for view '{viewRegistration.QueryViewType.FullName}' " +
                $"did not return '{typeof(QueryResult<TView>).FullName}'.");
        }

        return await typedTask;
    }

    private async Task<QueryResult<TView>> ExecuteDirectWithDiscoveredContextAsync<TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QueryContextRegistration contextRegistration,
        CancellationToken cancellationToken)
        where TView : class
    {
        var typedMethod =
            Close(
                ExecuteDirectTypedAsyncMethod,
                contextRegistration.ContextType,
                typeof(TView));

        var result =
            typedMethod.Invoke(
                this,
                [serviceProvider, request, contextRegistration, cancellationToken]);

        if (result is not Task<QueryResult<TView>> typedTask)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Direct query execution for context '{contextRegistration.ContextType.FullName}' " +
                $"did not return '{typeof(QueryResult<TView>).FullName}'.");
        }

        return await typedTask;
    }

    private static async Task<QueryResult<TView>> ExecuteTypedAsync<TContext, TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QueryContextRegistration contextRegistration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TContext : class
        where TView : class
    {
        var engine =
            serviceProvider.GetRequiredService<
                IQueryContextEngine<TContext, TView>>();

        return await engine.ExecuteAsync(
            request,
            contextRegistration,
            viewRegistration,
            cancellationToken);
    }

    private static async Task<QueryResult<TView>> ExecuteDelegatedTypedAsync<TContext, TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        DelegatedQueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TContext : class
        where TView : class
    {
        var engine =
            serviceProvider.GetRequiredService<
                IDelegatedQueryViewEngine<TContext, TView>>();

        return await engine.ExecuteAsync(
            request,
            viewRegistration,
            cancellationToken);
    }

    private static async Task<QueryResult<TView>> ExecuteDirectTypedAsync<TContext, TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        QueryContextRegistration contextRegistration,
        CancellationToken cancellationToken)
        where TContext : class
        where TView : class
    {
        var engine =
            serviceProvider.GetRequiredService<
                IQueryContextEngine<TContext, TView>>();

        return await engine.ExecuteAsync(
            request,
            contextRegistration,
            cancellationToken);
    }

    private async Task<QueryResult<TView>> ExecuteDelegatedViewAsync<TView>(
        IServiceProvider serviceProvider,
        IQueryRequest request,
        DelegatedQueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TView : class
    {
        var typedMethod =
            Close(
                ExecuteDelegatedTypedAsyncMethod,
                viewRegistration.QueryContextType,
                typeof(TView));

        var result =
            typedMethod.Invoke(
                this,
                [serviceProvider, request, viewRegistration, cancellationToken]);

        if (result is not Task<QueryResult<TView>> typedTask)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query execution for view '{viewRegistration.QueryViewType.FullName}' did not return '{typeof(QueryResult<TView>).FullName}'.");
        }

        return await typedTask;
    }

    private static void ValidateDelegatedViewRegistration<TQueryView, TView>(
        DelegatedQueryViewRegistration viewRegistration)
        where TQueryView : class
        where TView : class
    {
        if (viewRegistration.QueryViewType != typeof(TQueryView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query view registration mismatch. Requested query view " +
                $"'{typeof(TQueryView).FullName}', but registration contains " +
                $"'{viewRegistration.QueryViewType.FullName}'.");
        }

        if (viewRegistration.ViewType != typeof(TView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query view '{viewRegistration.QueryViewType.FullName}' returns " +
                $"'{viewRegistration.ViewType.FullName}', but query requested " +
                $"'{typeof(TView).FullName}'.");
        }
    }

    private static void ValidateViewRegistration<TQueryView, TView>(
        QueryViewRegistration viewRegistration)
        where TQueryView : class
        where TView : class
    {
        if (viewRegistration.QueryViewType != typeof(TQueryView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query view registration mismatch. Requested query view " +
                $"'{typeof(TQueryView).FullName}', but registration contains " +
                $"'{viewRegistration.QueryViewType.FullName}'.");
        }

        if (viewRegistration.ViewType != typeof(TView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query view '{viewRegistration.QueryViewType.FullName}' returns " +
                $"'{viewRegistration.ViewType.FullName}', but query requested " +
                $"'{typeof(TView).FullName}'.");
        }

        if (viewRegistration.QueryContextType is null)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query view '{viewRegistration.QueryViewType.FullName}' does not define a query context type.");
        }
    }

    private static void ValidateDirectQuery<TQueryView, TView>(
        QueryContextRegistration contextRegistration)
        where TQueryView : class
        where TView : class
    {
        if (contextRegistration.Metadata.Kind != QueryContextKind.Direct)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query context '{contextRegistration.ContextType.FullName}' does not allow direct query.");
        }

        if (contextRegistration.ContextType != typeof(TQueryView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query context registration mismatch. Requested query context " +
                $"'{typeof(TQueryView).FullName}', but registration contains " +
                $"'{contextRegistration.ContextType.FullName}'.");
        }

        if (contextRegistration.ContextType != typeof(TView))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Direct query for context '{contextRegistration.ContextType.FullName}' must return " +
                $"the same type, but query requested '{typeof(TView).FullName}'.");
        }
    }
}
