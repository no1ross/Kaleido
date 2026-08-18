using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

internal sealed class QueryableService : IQueryableService
{
    private static readonly MethodInfo ExecuteTypedAsyncMethod =
        typeof(QueryableService)
            .GetMethod(
                nameof(ExecuteTypedAsync),
                BindingFlags.Instance |
                BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"Could not locate method '{nameof(ExecuteTypedAsync)}'.");

    private static readonly MethodInfo ExecuteDirectTypedAsyncMethod =
        typeof(QueryableService)
            .GetMethod(
                nameof(ExecuteDirectTypedAsync),
                BindingFlags.Instance |
                BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"Could not locate method '{nameof(ExecuteDirectTypedAsync)}'.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQueryViewRegistry _viewRegistry;
    private readonly IQueryContextRegistry _contextRegistry;

    public QueryableService(
        IServiceScopeFactory scopeFactory,
        IQueryViewRegistry viewRegistry,
        IQueryContextRegistry contextRegistry)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(viewRegistry);
        ArgumentNullException.ThrowIfNull(contextRegistry);

        _scopeFactory =
            scopeFactory;

        _viewRegistry =
            viewRegistry;

        _contextRegistry =
            contextRegistry;
    }

    public async Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(
        IQueryRequest request,
        CancellationToken cancellationToken = default)
        where TQueryView : class
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewRegistration =
            _viewRegistry.Find(
                typeof(TQueryView));

        using var scope =
            _scopeFactory.CreateScope();

        if (viewRegistration is not null)
        {
            ValidateViewRegistration<TQueryView, TView>(
                viewRegistration);

            var contextRegistration =
                _contextRegistry.GetRegistration(
                    viewRegistration.QueryContextType);

            return await ExecuteWithDiscoveredContextAsync<TView>(
                scope.ServiceProvider,
                request,
                contextRegistration,
                viewRegistration,
                cancellationToken);
        }

        var directRegistration =
            _contextRegistry.GetRegistration(
                typeof(TQueryView));

        ValidateDirectQuery<TQueryView, TView>(
            directRegistration);

        return await ExecuteDirectWithDiscoveredContextAsync<TView>(
            scope.ServiceProvider,
            request,
            directRegistration,
            cancellationToken);
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
            ExecuteTypedAsyncMethod.MakeGenericMethod(
                viewRegistration.QueryContextType,
                typeof(TView));

        var result =
            typedMethod.Invoke(
                this,
                new object[]
                {
                    serviceProvider,
                    request,
                    contextRegistration,
                    viewRegistration,
                    cancellationToken
                });

        if (result is not Task<QueryResult<TView>> typedTask)
        {
            throw new InvalidOperationException(
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
            ExecuteDirectTypedAsyncMethod.MakeGenericMethod(
                contextRegistration.ContextType,
                typeof(TView));

        var result =
            typedMethod.Invoke(
                this,
                new object[]
                {
                    serviceProvider,
                    request,
                    contextRegistration,
                    cancellationToken
                });

        if (result is not Task<QueryResult<TView>> typedTask)
        {
            throw new InvalidOperationException(
                $"Direct query execution for context '{contextRegistration.ContextType.FullName}' " +
                $"did not return '{typeof(QueryResult<TView>).FullName}'.");
        }

        return await typedTask;
    }

    private async Task<QueryResult<TView>> ExecuteTypedAsync<TContext, TView>(
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

    private async Task<QueryResult<TView>> ExecuteDirectTypedAsync<TContext, TView>(
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

    private static void ValidateViewRegistration<TQueryView, TView>(
        QueryViewRegistration viewRegistration)
        where TQueryView : class
        where TView : class
    {
        if (viewRegistration.QueryViewType != typeof(TQueryView))
        {
            throw new InvalidOperationException(
                $"Query view registration mismatch. Requested query view " +
                $"'{typeof(TQueryView).FullName}', but registration contains " +
                $"'{viewRegistration.QueryViewType.FullName}'.");
        }

        if (viewRegistration.ViewType != typeof(TView))
        {
            throw new InvalidOperationException(
                $"Query view '{viewRegistration.QueryViewType.FullName}' returns " +
                $"'{viewRegistration.ViewType.FullName}', but query requested " +
                $"'{typeof(TView).FullName}'.");
        }

        if (viewRegistration.QueryContextType is null)
        {
            throw new InvalidOperationException(
                $"Query view '{viewRegistration.QueryViewType.FullName}' does not define a query context type.");
        }
    }

    private static void ValidateDirectQuery<TQueryView, TView>(
        QueryContextRegistration contextRegistration)
        where TQueryView : class
        where TView : class
    {
        if (!contextRegistration.Metadata.AllowDirectQuery)
        {
            throw new InvalidOperationException(
                $"Query context '{contextRegistration.ContextType.FullName}' does not allow direct query.");
        }

        if (contextRegistration.ContextType != typeof(TQueryView))
        {
            throw new InvalidOperationException(
                $"Query context registration mismatch. Requested query context " +
                $"'{typeof(TQueryView).FullName}', but registration contains " +
                $"'{contextRegistration.ContextType.FullName}'.");
        }

        if (contextRegistration.ContextType != typeof(TView))
        {
            throw new InvalidOperationException(
                $"Direct query for context '{contextRegistration.ContextType.FullName}' must return " +
                $"the same type, but query requested '{typeof(TView).FullName}'.");
        }
    }
}
