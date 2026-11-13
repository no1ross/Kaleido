namespace Kaleido.Queryable.Http;

// ── Request ──────────────────────────────────────────────────────────────────

/// <summary>
/// The body of a query endpoint for a query without parameters: <c>{"query":{…}}</c>. Used by
/// clients to query a source directly when it takes no parameters.
/// </summary>
/// <param name="Query">The search, filter, sort and page to apply; <see langword="null"/> for none of them.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryApiRequest(
    QueryApiBody? Query = null);

/// <summary>
/// The body of a query endpoint (<c>POST /{service}/{version}/queryable/{source}[/{view}]/query</c>):
/// the source's or view's parameters plus an optional query, <c>{"parameters":{…},"query":{…}}</c>.
/// </summary>
/// <typeparam name="TParameters">
/// The parameters record of the source or view: the declared type on the server; on a client, that
/// type or a local mirror.
/// </typeparam>
/// <param name="Parameters">The parameter values; <see langword="null"/> when none are supplied.</param>
/// <param name="Query">The search, filter, sort and page to apply; <see langword="null"/> for none of them.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryApiBody? Query = null)
    where TParameters : class;
