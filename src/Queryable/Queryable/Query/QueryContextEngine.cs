using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Query;

internal sealed class QueryContextEngine<TQueryContext, TView>(
    IQueryContextValidator validator,
    IQueryContextCompiler compiler,
    IQueryContextSource<TQueryContext> source,
    ICompiledQueryApplier<TQueryContext> applier,
    IQueryContextExecutor<TView> executor,
    IQueryEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider) : IQueryContextEngine<TQueryContext, TView>
    where TQueryContext : class
    where TView : class
{

    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                registration.Metadata.Name,
                viewRegistration.Metadata.Name,
                false,
                QueryExecutionMode.LocalView);

        using var observation =
            observability.BeginExecution(
                details);

        try
        {
            var metadata = registration.Metadata;
            validator.Validate(request, registration, viewRegistration);

            var executionContext = new QueryExecutionContext(metadata, request);
            var compiled = compiler.Compile(request, metadata, viewRegistration.Metadata);
            var query = CreateQuery(executionContext, compiled, observation);
            var view = CreateView(viewRegistration, query, executionContext, observation);
            var result = await MaterializeAsync(
                view,
                compiled.Page,
                viewRegistration.Metadata.Pageable is not null,
                observation,
                cancellationToken);

            await eventPublisher.PublishAsync(
                eventFactory.CreateQueryExecuted(
                    correlationAccessor.Current,
                    details,
                    request,
                    result),
                cancellationToken);

            return result;
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
        var details =
            new QueryObservationDetails(
                registration.Metadata.Name,
                null,
                true,
                QueryExecutionMode.DirectContext);

        using var observation =
            observability.BeginExecution(
                details);

        try
        {
            var metadata = registration.Metadata;
            validator.Validate(request, registration);

            var executionContext = new QueryExecutionContext(metadata, request);
            var compiled = compiler.Compile(request, metadata);
            var query = CreateQuery(executionContext, compiled, observation);

            if (query is not IQueryable<TView> typedQuery)
            {
                throw new InvalidOperationException(
                    $"Direct query for context '{typeof(TQueryContext).FullName}' requires result type '{typeof(TView).FullName}' to match the query context type.");
            }

            var result = await MaterializeAsync(
                typedQuery,
                compiled.Page,
                metadata.Pageable is not null,
                observation,
                cancellationToken);

            await eventPublisher.PublishAsync(
                eventFactory.CreateQueryExecuted(
                    correlationAccessor.Current,
                    details,
                    request,
                    result),
                cancellationToken);

            return result;
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

        var query = source.CreateQuery(executionContext);

        query = applier.ApplySearch(query, compiled.Search);
        query = applier.ApplyFilter(query, compiled.Filter);
        query = applier.ApplySort(query, compiled.Sort);

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

        var totalCount = await executor.CountAsync(query, cancellationToken);

        if (pageable)
        {
            query = executor.ApplyPage(query, page);
        }

        var items = await executor.ToListAsync(query, cancellationToken);

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
            serviceProvider.GetRequiredService(
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
