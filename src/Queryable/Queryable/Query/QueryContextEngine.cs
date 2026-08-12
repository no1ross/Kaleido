using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Reflection;

namespace Kaleido.Queryable.Query;

internal sealed class QueryContextEngine<TQueryContext, TView> : IQueryContextEngine<TQueryContext, TView>
    where TQueryContext : class
    where TView : class
{
    private readonly IQueryContextValidator _validator;
    private readonly IQueryContextCompiler _compiler;
    private readonly IQueryContextSource<TQueryContext> _source;
    private readonly ICompiledQueryApplier<TQueryContext> _applier;
    private readonly IQueryContextExecutor<TView> _executor;
    private readonly IServiceProvider _serviceProvider;

    public QueryContextEngine(
        IQueryContextValidator validator,
        IQueryContextCompiler compiler,
        IQueryContextSource<TQueryContext> source,
        ICompiledQueryApplier<TQueryContext> applier,
        IQueryContextExecutor<TView> executor,
        IServiceProvider serviceProvider)
    {
        _validator = validator;
        _compiler = compiler;
        _source = source;
        _applier = applier;
        _executor = executor;
        _serviceProvider = serviceProvider;
    }

    public async Task<QueryResult<TView>> ExecuteAsync(IQueryRequest request, 
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration, 
        CancellationToken cancellationToken = default)
    {
        var metadata = registration.Metadata;
        _validator.Validate(request, registration, viewRegistration);

        var executionContext = new QueryExecutionContext(metadata, request);

        var compiled = _compiler.Compile(request, metadata, viewRegistration.Metadata);

        var query = _source.CreateQuery(executionContext);

        query = _applier.ApplySearch(query, compiled.Search);
        query = _applier.ApplyFilter(query, compiled.Filter);
        query = _applier.ApplySort(query, compiled.Sort);

        var view = CreateView(viewRegistration, query, executionContext);

        var totalCount = await _executor.CountAsync(view, cancellationToken);

        if (viewRegistration.Metadata.Pageable is not null)
        {
            view = _executor.ApplyPage(view, compiled.Page);
        }        
        
        var items = await _executor.ToListAsync(view, cancellationToken);

        return new QueryResult<TView>(totalCount, compiled.Page?.Offset ?? 0, compiled.Page?.Size ?? int.MaxValue, items);
    }

    private IQueryable<TView> CreateView(
        QueryViewRegistration viewRegistration,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var queryView =
            _serviceProvider.GetRequiredService(
                viewRegistration.QueryViewType);

        var typedMethod =
            CreateViewTypedMethod.MakeGenericMethod(
                viewRegistration.ViewParametersType);

        var result =
            typedMethod.Invoke(
                this,
                new object[]
                {
                    queryView,
                    query,
                    executionContext,
                    viewRegistration
                });

        if (result is IQueryable<TView> typedView)
        {
            return typedView;
        }

        throw new InvalidOperationException(
            $"Query view '{viewRegistration.QueryViewType.FullName}' did not return " +
            $"'{typeof(IQueryable<TView>).FullName}'.");
    }

    private IQueryable<TView> CreateViewTyped<TViewParameters>(
        object queryView,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        QueryViewRegistration viewRegistration)
        where TViewParameters : class
    {
        if (queryView is not IQueryViewSource<TQueryContext, TView, TViewParameters> typedQueryView)
        {
            throw new InvalidOperationException(
                $"Query view '{viewRegistration.QueryViewType.FullName}' must implement " +
                $"'{typeof(IQueryViewSource<TQueryContext, TView, TViewParameters>).FullName}'.");
        }

        return typedQueryView.CreateView(
            query,
            executionContext);
    }

    private static readonly MethodInfo CreateViewTypedMethod =
        typeof(QueryContextEngine<TQueryContext, TView>)
            .GetMethod(
                nameof(CreateViewTyped),
                BindingFlags.Instance |
                BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"Unable to locate method '{nameof(CreateViewTyped)}'.");
}
