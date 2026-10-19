using System.Reflection;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Executes queries against a local source: direct source queries and local view queries.
/// </summary>
internal interface IQueryContextEngine<TQueryContext, TView>
        where TQueryContext : class, IQueryContext
        where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QuerySourceRegistration source,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default);

    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken = default);
}

internal sealed class QueryContextEngine<TQueryContext, TView>(
    IQueryContextValidator validator,
    IQueryContextCompiler compiler,
    ICompiledQueryApplier<TQueryContext> applier,
    IQueryContextExecutor<TView> executor,
    IQueryEventFactory eventFactory,
    IKaleidoEventOutbox eventOutbox,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider) : IQueryContextEngine<TQueryContext, TView>
    where TQueryContext : class, IQueryContext
    where TView : class
{

    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QuerySourceRegistration source,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                source.Metadata.Name,
                viewRegistration.Metadata.Name,
                IsDirectQuery: false,
                QueryExecutionMode.LocalView);

        using var observation =
            observability.BeginExecution(
                details);

        try
        {
            var metadata = source.Metadata;
            var pageable = viewRegistration.Metadata.Pageable;
            validator.Validate(request, metadata, pageable);

            var executionContext = new QueryExecutionContext(request);
            var compiled = compiler.Compile(request, metadata, pageable);
            var query = await CreateQueryAsync(source, executionContext, compiled, observation, cancellationToken);
            var view = await CreateViewAsync(viewRegistration, query, executionContext, observation, cancellationToken);
            var result = await MaterializeAsync(
                view,
                compiled.Page,
                pageable is not null,
                observation,
                cancellationToken);

            await EnqueueExecutedAsync(details, request, result);

            return result;
        }
        catch (KaleidoValidationException exception)
        {
            observation.ValidationFailed(exception);
            throw;
        }
        catch (OperationCanceledException)
        {
            observation.Canceled();
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
        QuerySourceRegistration source,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                source.Metadata.Name,
                QueryViewName: null,
                IsDirectQuery: true,
                QueryExecutionMode.DirectSource);

        using var observation =
            observability.BeginExecution(
                details);

        try
        {
            var metadata = source.Metadata;
            validator.Validate(request, metadata, metadata.Pageable);

            var executionContext = new QueryExecutionContext(request);
            var compiled = compiler.Compile(request, metadata, metadata.Pageable);
            var query = await CreateQueryAsync(source, executionContext, compiled, observation, cancellationToken);

            if (query is not IQueryable<TView> typedQuery)
            {
                throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Direct query for source '{source.SourceType.FullName}' requires result type '{typeof(TView).FullName}' to match the query context type '{typeof(TQueryContext).FullName}'.");
            }

            var result = await MaterializeAsync(
                typedQuery,
                compiled.Page,
                metadata.Pageable is not null,
                observation,
                cancellationToken);

            await EnqueueExecutedAsync(details, request, result);

            return result;
        }
        catch (KaleidoValidationException exception)
        {
            observation.ValidationFailed(exception);
            throw;
        }
        catch (OperationCanceledException)
        {
            observation.Canceled();
            throw;
        }
        catch (Exception exception)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    // QueryExecuted goes to the outbox (nothing is written without a registered publisher).
    private Task EnqueueExecutedAsync(
        QueryObservationDetails details,
        IQueryRequest request,
        QueryResult<TView> result) =>
        eventOutbox.EnqueueAsync(
            eventFactory.CreateQueryExecuted(correlationAccessor.Current, details, request, result),
            CancellationToken.None);

    private async Task<IQueryable<TQueryContext>> CreateQueryAsync(
        QuerySourceRegistration source,
        QueryExecutionContext executionContext,
        CompiledQuery compiled,
        IQueryExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        using var scope =
            observation.BeginSource();

        var instance =
            serviceProvider.GetRequiredService(
                source.SourceType);

        var query = instance switch
        {
            IQuerySourceAsync<TQueryContext> asyncSource =>
                await asyncSource.CreateQueryAsync(executionContext, cancellationToken),
            IQuerySource<TQueryContext> syncSource =>
                syncSource.CreateQuery(executionContext),
            _ => throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Query source '{source.SourceType.FullName}' must implement " +
                $"IQuerySource<{typeof(TQueryContext).Name}> or IQuerySourceAsync<{typeof(TQueryContext).Name}>."),
        };

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

        // Capture the unpaged query before ApplyPage so CountAsync counts the
        // full result set, not just the requested page.
        var unpagedQuery = query;

        if (pageable)
        {
            query = executor.ApplyPage(query, page.Size, page.Offset);
        }

        var items = await executor.ToListAsync(query, cancellationToken);

        var totalCount = await TotalCountAsync(unpagedQuery, items.Count, page, pageable, cancellationToken);

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

    // The number of matching results, whatever page was returned. A pageable capability always
    // pages (its default page when the request names none). A partial page ends the results, so
    // the total is its offset plus its rows; a full page, or an empty one past the start, may not,
    // so the results are counted. An unpageable capability returns every result.
    private async Task<int> TotalCountAsync(
        IQueryable<TView> unpagedQuery,
        int returned,
        CompiledPage page,
        bool pageable,
        CancellationToken cancellationToken) =>
        !pageable
            ? returned
            : returned == page.Size || (returned == 0 && page.Offset > 0)
                ? await executor.CountAsync(unpagedQuery, cancellationToken)
                : page.Offset + returned;

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
            ClosedViewMethods.GetOrAdd(
                (viewRegistration.SourceType, viewRegistration.ViewParametersType),
                key => CreateViewAsyncTypedMethod.MakeGenericMethod(key.Source, key.Parameters));

        var task = (Task<IQueryable<TView>>)(typedMethod.Invoke(
            this,
            [queryView, query, executionContext, viewRegistration, cancellationToken])
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(CreateViewAsyncTyped)}' returned null for view '{viewRegistration.QueryViewType.FullName}'."));

        return await task;
    }

    private static async Task<IQueryable<TView>> CreateViewAsyncTyped<TSource, TViewParameters>(
        object queryView,
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken)
        where TSource : class, ILocalQuerySource<TQueryContext>
        where TViewParameters : class, IQueryParameters
    {
        if (queryView is IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters> asyncView)
        {
            return await asyncView.CreateViewAsync(query, executionContext, cancellationToken);
        }

        if (queryView is IQueryViewSource<TSource, TQueryContext, TView, TViewParameters> syncView)
        {
            return syncView.CreateView(query, executionContext);
        }

        throw new KaleidoFrameworkException(
            FrameworkErrorCodes.TypeMismatch,
            $"Query view '{viewRegistration.QueryViewType.FullName}' must implement " +
            $"'{typeof(IQueryViewSource<TSource, TQueryContext, TView, TViewParameters>).FullName}' or " +
            $"'{typeof(IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters>).FullName}'.");
    }

    // Closed-generic cache — MakeGenericMethod allocates per call and this
    // executes once per query request.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Source, Type Parameters), MethodInfo> ClosedViewMethods =
        new();

    private static readonly MethodInfo CreateViewAsyncTypedMethod =
        typeof(QueryContextEngine<TQueryContext, TView>)
            .GetMethod(
                nameof(CreateViewAsyncTyped),
                BindingFlags.Static |
                BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Unable to locate method '{nameof(CreateViewAsyncTyped)}'.");
}
