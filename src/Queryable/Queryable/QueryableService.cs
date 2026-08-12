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
            _viewRegistry.GetRegistration(
                typeof(TQueryView));

        ValidateViewRegistration<TQueryView, TView>(
            viewRegistration);

        var contextRegistration =
            _contextRegistry.GetRegistration(
                viewRegistration.QueryContextType);

        using var scope =
            _scopeFactory.CreateScope();

        return await ExecuteWithDiscoveredContextAsync<TView>(
            scope.ServiceProvider,
            request,
            contextRegistration,
            viewRegistration,
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
}