using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    IEventPublisher eventPublisher,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider,
    ILogger<DelegatedQuerySourceEngine<TQueryContext, TResult>> logger)
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
                null,
                true,
                QueryExecutionMode.DelegatedSource);

        using var observation =
            observability.BeginExecution(details);

        try
        {
            if (request.ViewParametersType != source.ParametersType)
            {
                throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Delegated query source '{source.SourceType.FullName}' expected parameters '{source.ParametersType.FullName}', but request used '{request.ViewParametersType.FullName}'.");
            }

            validator.Validate(request, source.Metadata, source.Metadata.Pageable);

            var instance =
                serviceProvider.GetRequiredService(
                    source.SourceType);

            var typedMethod =
                ClosedMethods.GetOrAdd(
                    source.ParametersType,
                    key => ExecuteTypedAsyncMethod.MakeGenericMethod(key));

            using var scope = observation.BeginDelegate();

            var invocation = typedMethod.Invoke(this, [instance, request, source, cancellationToken]);

            if (invocation is not Task<QueryResult<TResult>> typedTask)
            {
                throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Delegated query execution for source '{source.SourceType.FullName}' did not return '{typeof(QueryResult<TResult>).FullName}'.");
            }

            var result = await typedTask;

            observation.Materialized(
                result.TotalCount,
                result.Results.Count,
                result.PageSize,
                result.Offset);

            if (eventPublisher is not EventPublisher)
            {
                try
                {
                    _ = eventPublisher.PublishAsync(
                        eventFactory.CreateQueryExecuted(
                            correlationAccessor.Current,
                            details,
                            request,
                            result),
                        cancellationToken);
                }
                catch (Exception publishException) when (publishException is not OperationCanceledException)
                {
                    logger.LogWarning(
                        publishException,
                        "Event publish failed for QueryExecuted on source {QuerySourceName}. Event delivery is best-effort.",
                        details.QuerySourceName);
                }
            }

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
