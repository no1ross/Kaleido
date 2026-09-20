using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;

namespace Kaleido.Queryable.Query;

internal interface IQueryContextEngine<TQueryContext, TView>
        where TQueryContext : class
        where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default);

    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        CancellationToken cancellationToken = default);
}

internal sealed class QueryContextEngine<TQueryContext, TView>(
    IQueryContextValidator validator,
    IQueryContextCompiler compiler,
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
            var query = await CreateQueryAsync(executionContext, compiled, observation, cancellationToken);
            var view = await CreateViewAsync(viewRegistration, query, executionContext, observation, cancellationToken);
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
            var query = await CreateQueryAsync(executionContext, compiled, observation, cancellationToken);

            if (query is not IQueryable<TView> typedQuery)
            {
                throw new KaleidoFrameworkException(
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

    private async Task<IQueryable<TQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CompiledRecordQuery compiled,
        IQueryExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        using var scope =
            observation.BeginSource();

        var syncSource = serviceProvider.GetService<IQueryContextSource<TQueryContext>>();
        var asyncSource = serviceProvider.GetService<IQueryContextSourceAsync<TQueryContext>>();

        var query = asyncSource is not null
            ? await asyncSource.CreateQueryAsync(executionContext, cancellationToken)
            : syncSource?.CreateQuery(executionContext)
            ?? throw new QueryContextSourceNotFoundException(typeof(TQueryContext));

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

    private async Task<IQueryable<TView>> CreateViewAsync(
        QueryViewRegistration viewRegistration,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        IQueryExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        using var scope =
            observation.BeginView();

        var queryView =
            serviceProvider.GetRequiredService(
                viewRegistration.QueryViewType);

        var typedMethod =
            CreateViewAsyncTypedMethod.MakeGenericMethod(
                viewRegistration.ViewParametersType);

        var task = (Task<IQueryable<TView>>)typedMethod.Invoke(
            this,
            new object[]
            {
                queryView,
                query,
                executionContext,
                viewRegistration,
                cancellationToken
            })!;

        return await task;
    }

    private async Task<IQueryable<TView>> CreateViewAsyncTyped<TViewParameters>(
        object queryView,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TViewParameters : class
    {
        if (queryView is IQueryViewSourceAsync<TQueryContext, TView, TViewParameters> asyncView)
        {
            return await asyncView.CreateViewAsync(query, executionContext, cancellationToken);
        }

        if (queryView is IQueryViewSource<TQueryContext, TView, TViewParameters> syncView)
        {
            return syncView.CreateView(query, executionContext);
        }

        throw new KaleidoFrameworkException(
            $"Query view '{viewRegistration.QueryViewType.FullName}' must implement " +
            $"'{typeof(IQueryViewSource<TQueryContext, TView, TViewParameters>).FullName}' or " +
            $"'{typeof(IQueryViewSourceAsync<TQueryContext, TView, TViewParameters>).FullName}'.");
    }

    private static readonly MethodInfo CreateViewAsyncTypedMethod =
        typeof(QueryContextEngine<TQueryContext, TView>)
            .GetMethod(
                nameof(CreateViewAsyncTyped),
                BindingFlags.Instance |
                BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"Unable to locate method '{nameof(CreateViewAsyncTyped)}'.");
}
