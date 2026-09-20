using Kaleido.Queryable.Query;

namespace Kaleido.Http.Abstractions.Queryable.Contracts;

public record QueryApiRequest(
    QueryBody? Query = null);

public record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryBody? Query = null)
    where TParameters : class;
