using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Query;

internal sealed class DelegatedQueryViewEngine<TDelegateContext, TView>(
    IQueryEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider)
    : IDelegatedQueryViewEngine<TDelegateContext, TView>
    where TDelegateContext : class
    where TView : class
{
    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        DelegatedQueryViewRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                registration.QueryMetadata.Name,
                registration.ViewMetadata.Name,
                false,
                QueryExecutionMode.DelegatedContext);

        using var observation =
            observability.BeginExecution(details);

        try
        {
            var source =
                serviceProvider.GetRequiredService(
                    registration.QueryViewType);

            if (request.ViewParametersType != registration.ViewParametersType)
            {
                throw new InvalidOperationException(
                    $"Delegated query view '{registration.QueryViewType.FullName}' expected parameters '{registration.ViewParametersType.FullName}', but request used '{request.ViewParametersType.FullName}'.");
            }

            var typedMethod =
                typeof(DelegatedQueryViewEngine<TDelegateContext, TView>)
                    .GetMethod(nameof(ExecuteTypedAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(registration.ViewParametersType);

            using var scope = observation.BeginDelegate();

            var invocation = typedMethod.Invoke(this, [source, request, registration, cancellationToken]);

            if (invocation is not Task<QueryResult<TView>> typedTask)
            {
                throw new InvalidOperationException(
                    $"Delegated query execution for view '{registration.QueryViewType.FullName}' did not return '{typeof(QueryResult<TView>).FullName}'.");
            }

            var result = await typedTask;

            observation.Materialized(
                result.TotalCount,
                result.Results.Count,
                result.PageSize,
                result.Offset);

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

    private Task<QueryResult<TView>> ExecuteTypedAsync<TParameters>(
        object source,
        IQueryRequest request,
        DelegatedQueryViewRegistration registration,
        CancellationToken cancellationToken)
        where TParameters : class
    {
        if (source is not IDelegateQueryViewSource<TDelegateContext, TView, TParameters> delegatedSource)
        {
            throw new InvalidOperationException(
                $"Delegated query view '{registration.QueryViewType.FullName}' must implement '{typeof(IDelegateQueryViewSource<TDelegateContext, TView, TParameters>).FullName}'.");
        }

        if (request is not IQueryRequest<TParameters> typedRequest)
        {
            throw new InvalidOperationException(
                $"Delegated query view '{registration.QueryViewType.FullName}' expected request type '{typeof(IQueryRequest<TParameters>).FullName}'.");
        }

        return delegatedSource.ExecuteAsync(
            typedRequest,
            cancellationToken);
    }
}
