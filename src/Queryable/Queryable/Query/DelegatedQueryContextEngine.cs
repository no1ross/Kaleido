using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Query;

internal sealed class DelegatedQueryContextEngine<TQueryContext, TView>(
    IQueryEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IQueryableObservability observability,
    IServiceProvider serviceProvider)
    : IDelegatedQueryContextEngine<TQueryContext, TView>
    where TQueryContext : class
    where TView : class
{
    public async Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var details =
            new QueryObservationDetails(
                registration.Metadata.Name,
                null,
                false,
                QueryExecutionMode.DelegatedContext);

        using var observation =
            observability.BeginExecution(details);

        try
        {
            var source =
                serviceProvider.GetRequiredService(
                    registration.SourceType);

            if (source is not IDelegatedQueryContextSource<TQueryContext, TView> delegatedSource)
            {
                throw new InvalidOperationException(
                    $"Delegated source '{registration.SourceType.FullName}' must implement '{typeof(IDelegatedQueryContextSource<TQueryContext, TView>).FullName}'.");
            }

            using var scope =
                observation.BeginDelegate();

            var result =
                await delegatedSource.ExecuteAsync(
                        request,
                        cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Delegated source '{registration.SourceType.FullName}' returned null.");

            observation.Materialized(
                result.TotalCount,
                result.Records.Count,
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
}
