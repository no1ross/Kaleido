using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// What a local query source or view receives about the query being executed: the source's
/// metadata and the current request.
/// </summary>
/// <remarks>
/// Passed to <see cref="IQuerySource{TQueryContext}.CreateQuery"/>,
/// <see cref="IQuerySourceAsync{TQueryContext}.CreateQueryAsync"/> and the query view
/// <c>CreateView</c>/<c>CreateViewAsync</c> methods after the request has been validated. Kaleido
/// applies search, filter, sort and paging itself; sources and views read the parameters through
/// <see cref="TryGetViewParameters{TViewParameters}"/>.
/// </remarks>
/// <param name="Metadata">
/// The metadata of the query source being queried (also for a view query, where it is the view's
/// source).
/// </param>
/// <param name="Request">The validated request being executed.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryExecutionContext
(
    QuerySourceMetadata Metadata,
    IQueryRequest Request
)
{
    /// <summary>Gets the request's parameters record as <typeparamref name="TViewParameters"/>.</summary>
    /// <typeparam name="TViewParameters">The expected parameters record type.</typeparam>
    /// <returns>
    /// The parameters record, or <see langword="null"/> when the request carries none.
    /// </returns>
    /// <exception cref="KaleidoValidationException">
    /// The request's parameters record is not a <typeparamref name="TViewParameters"/>
    /// (<see cref="QueryableErrorCodes.InvalidParameterValue"/>).
    /// </exception>
    public TViewParameters? TryGetViewParameters<TViewParameters>()
        where TViewParameters : class
    {
        if (Request.ViewParameters is null)
        {
            return null;
        }

        if (Request.ViewParameters is TViewParameters parameters)
        {
            return parameters;
        }

        throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidParameterValue,
            $"ViewParameters is of type '{Request.ViewParameters.GetType().Name}' but expected '{typeof(TViewParameters).Name}'.");
    }
}
