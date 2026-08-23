using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IQueryableObservability _observability;
    private readonly IServiceProvider _serviceProvider;

    public QueryContextEngine(
        IQueryContextValidator validator,
        IQueryContextCompiler compiler,
        IQueryContextSource<TQueryContext> source,
        ICompiledQueryApplier<TQueryContext> applier,
        IQueryContextExecutor<TView> executor,
        IQueryableObservability observability,
        IServiceProvider serviceProvider)
    {
        _validator = validator;
        _compiler = compiler;
        _source = source;
        _applier = applier;
        _executor = executor;
        _observability = observability;
        _serviceProvider = serviceProvider;
    }

    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default)
    {
        using var observation =
            _observability.BeginExecution(
                new QueryObservationDetails(
                    registration.Metadata.Name,
                    viewRegistration.Metadata.Name,
                    false));

        try
        {
            var metadata = registration.Metadata;
            _validator.Validate(request, registration, viewRegistration);

            var executionContext = new QueryExecutionContext(metadata, request);
            var compiled = _compiler.Compile(request, metadata, viewRegistration.Metadata);
            var query = CreateQuery(executionContext, compiled, observation);
            var view = CreateView(viewRegistration, query, executionContext, observation);

            return await MaterializeAsync(
                view,
                compiled.Page,
                viewRegistration.Metadata.Pageable is not null,
                observation,
                cancellationToken);
        }
        catch (QueryableValidationException exception)
        {
            observation.ValidationFailed(exception);
            throw;
        }
        catch (Exception exception)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        CancellationToken cancellationToken = default)
    {
        using var observation =
            _observability.BeginExecution(
                new QueryObservationDetails(
                    registration.Metadata.Name,
                    null,
                    true));

        try
        {
            var metadata = registration.Metadata;
            _validator.Validate(request, registration);

            var executionContext = new QueryExecutionContext(metadata, request);
            var compiled = _compiler.Compile(request, metadata);
            var query = CreateQuery(executionContext, compiled, observation);

            if (query is not IQueryable<TView> typedQuery)
            {
                throw new InvalidOperationException(
                    $"Direct query for context '{typeof(TQueryContext).FullName}' requires result type '{typeof(TView).FullName}' to match the query context type.");
            }

            return await MaterializeAsync(
                typedQuery,
                compiled.Page,
                metadata.Pageable is not null,
                observation,
                cancellationToken);
        }
        catch (QueryableValidationException exception)
        {
            observation.ValidationFailed(exception);
            throw;
        }
        catch (Exception exception)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    private IQueryable<TQueryContext> CreateQuery(
        QueryExecutionContext executionContext,
        CompiledRecordQuery compiled,
        IQueryExecutionObservation observation)
    {
        using var scope =
            observation.BeginSource();

        var query = _source.CreateQuery(executionContext);

        query = _applier.ApplySearch(query, compiled.Search);
        query = _applier.ApplyFilter(query, compiled.Filter);
        query = _applier.ApplySort(query, compiled.Sort);

        return query;
    }

    private async Task<QueryResult<TView>> MaterializeAsync(
        IQueryable<TView> query,
        CompiledPage page,
        bool pageable,
        IQueryExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        using var scope =
            observation.BeginMaterialization();

        var totalCount = await _executor.CountAsync(query, cancellationToken);

        if (pageable)
        {
            query = _executor.ApplyPage(query, page);
        }

        var items = await _executor.ToListAsync(query, cancellationToken);

        observation.Materialized(
            totalCount,
            items.Count,
            page.Size,
            page.Offset);

        return new QueryResult<TView>(
            totalCount,
            page.Offset,
            page.Size,
            items);
    }

    private IQueryable<TView> CreateView(
        QueryViewRegistration viewRegistration,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        IQueryExecutionObservation observation)
    {
        using var scope =
            observation.BeginView();

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
