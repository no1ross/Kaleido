using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public record QueryApiRequest(
    QueryBody? Query = null);

public record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryBody? Query = null)
    where TParameters : class;
