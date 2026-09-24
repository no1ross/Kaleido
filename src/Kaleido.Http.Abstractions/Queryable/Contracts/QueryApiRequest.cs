namespace Kaleido.Http.Queryable.Contracts;

[ExcludeFromCodeCoverage]
public record QueryApiRequest(
    QueryBody? Query = null);

[ExcludeFromCodeCoverage]
public record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryBody? Query = null)
    where TParameters : class;
