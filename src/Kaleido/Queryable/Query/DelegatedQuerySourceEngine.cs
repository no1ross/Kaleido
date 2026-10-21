using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Executes queries against a delegated source. The consumer's query is validated against the
/// source's query context (its public contract) and paging; the source then executes it
/// downstream and returns its own mapped results.
/// </summary>
internal interface IDelegatedQuerySourceEngine<TQueryContext, TResult>
    where TQueryContext : class, IQueryContext
    where TResult : class
{
    Task<QueryResult<TResult>> ExecuteAsync(
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken = default);
}

internal sealed class DelegatedQuerySourceEngine<TQueryContext, TResult>(
    IQueryContextValidator validator,
    IQueryEventFactory eventFactory,
    IKaleidoEventWriter eventWriter,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider)
    : IDelegatedQuerySourceEngine<TQueryContext, TResult>
    where TQueryContext : class, IQueryContext
    where TResult : class
{
    private static readonly System.Reflection.MethodInfo ExecuteTypedAsyncMethod =
        typeof(DelegatedQuerySourceEngine<TQueryContext, TResult>)
            .GetMethod(
                nameof(ExecuteTypedAsync),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(ExecuteTypedAsync)}' on DelegatedQuerySourceEngine.");

    // Closed-generic cache — MakeGenericMethod allocates per call and this
    // executes once per delegated query request.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo> ClosedMethods =
        new();

    public async Task<QueryResult<TResult>> ExecuteAsync(
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                source.Metadata.Name,
                QueryViewName: null,
                IsDirectQuery: true,
                QueryExecutionMode.DelegatedSource);

        using var observation =
            observability.BeginExecution(details);

        try
        {
            Validate(request, source);

            var instance =
                serviceProvider.GetRequiredService(
                    source.SourceType);

            var typedMethod =
                ClosedMethods.GetOrAdd(
                    source.ParametersType,
                    key => ExecuteTypedAsyncMethod.MakeGenericMethod(key));

            using var scope = observation.BeginDelegate();

            var result = await InvokeAsync(typedMethod, instance, request, source, cancellationToken);

            observation.Materialized(
                result.TotalCount,
                result.Results.Count,
                result.PageSize,
                result.Offset);

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

    // The request is validated against the delegated source's query context, like a local source's.
    private void Validate(IQueryRequest request, QuerySourceRegistration source)
    {
        if (request.ViewParametersType != source.ParametersType)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query source '{source.SourceType.FullName}' expected parameters '{source.ParametersType.FullName}', but request used '{request.ViewParametersType.FullName}'.");
        }

        validator.Validate(request, source.Metadata, source.Metadata.Pageable);
    }

    private Task<QueryResult<TResult>> InvokeAsync(
        System.Reflection.MethodInfo typedMethod,
        object instance,
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken) =>
        typedMethod.Invoke(this, [instance, request, source, cancellationToken]) as Task<QueryResult<TResult>>
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.TypeMismatch,
            $"Delegated query execution for source '{source.SourceType.FullName}' did not return '{typeof(QueryResult<TResult>).FullName}'.");

    // QueryExecuted goes to the outbox (nothing is written without a registered publisher).
    private Task EnqueueExecutedAsync(
        QueryObservationDetails details,
        IQueryRequest request,
        QueryResult<TResult> result) =>
        eventWriter.WriteAsync(
            eventFactory.CreateQueryExecuted(correlationAccessor.Current, details, request, result),
            CancellationToken.None);
    private static Task<QueryResult<TResult>> ExecuteTypedAsync<TParameters>(
        object instance,
        IQueryRequest request,
        QuerySourceRegistration source,
        CancellationToken cancellationToken)
        where TParameters : class, IQueryParameters
    {
        if (instance is not IDelegatedQuerySource<TQueryContext, TResult, TParameters> delegatedSource)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query source '{source.SourceType.FullName}' must implement '{typeof(IDelegatedQuerySource<TQueryContext, TResult, TParameters>).FullName}'.");
        }

        if (request is not IQueryRequest<TParameters> typedRequest)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Delegated query source '{source.SourceType.FullName}' expected request type '{typeof(IQueryRequest<TParameters>).FullName}'.");
        }

        return delegatedSource.ExecuteAsync(
            typedRequest,
            cancellationToken);
    }
}
