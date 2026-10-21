namespace Kaleido.Queryable.Query;

/// <summary>
/// A query request carrying a parameters record of type <typeparamref name="TParameters"/>.
/// </summary>
/// <typeparam name="TParameters">The parameters record of the query view or delegated source.</typeparam>
/// <param name="ViewParameters">
/// The parameters record, or <see langword="null"/> when none is supplied. Validated with its
/// DataAnnotations before the query runs: a missing required member fails with
/// <see cref="QueryableErrorCodes.MissingParameter"/>, any other failed annotation with
/// <see cref="QueryableErrorCodes.InvalidParameterValue"/>.
/// </param>
/// <param name="Query">The search, filter, sort and paging to apply, or <see langword="null"/> for none.</param>
public record QueryRequest<TParameters>(
    TParameters? ViewParameters,
    QueryBody? Query = null)
    : IQueryRequest<TParameters>
    where TParameters : class
{
    object? IQueryRequest.ViewParameters =>
        ViewParameters;

    Type IQueryRequest.ViewParametersType =>
        typeof(TParameters);
}
