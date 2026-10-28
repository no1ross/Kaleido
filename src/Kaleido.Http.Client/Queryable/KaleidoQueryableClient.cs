using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Queryable;

// One configured client's queries, as a keyed service (#498): the container passes the client name
// ([ServiceKey]); what to call is this class's, how to send it the transport's. Query URLs come from
// the route prefix, never from the registry: the registry is filtered per caller, so a cached copy
// would answer for whoever fetched it first (#381). An unknown or forbidden source or view gets the
// service's own answer (404 / 403); a 404 also drops the discovery snapshot.
internal sealed class KaleidoQueryableClient(
    [ServiceKey] string clientName,
    IKaleidoClientCatalog catalog,
    IKaleidoClientTransport transport,
    IKaleidoRemoteRegistry remoteRegistry,
    ICorrelationHeaderStamper headerStamper)
    : IKaleidoQueryableClient
{
    private readonly KaleidoClientDefinition _client = catalog.Require(clientName);

    public async Task<IReadOnlyList<QueryableSourceResponse>> GetRegistryAsync(CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(_client, headerStamper, cancellationToken)).Queryables];

    public Task InvalidateRegistryAsync(CancellationToken cancellationToken) =>
        remoteRegistry.InvalidateAsync(_client.Name, cancellationToken);

    public Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TView : class =>
        QueryAsync<TView>(
            QueryableContractUrls.QueryViewQuery(_client.RoutePrefix, Segment(source), Segment(view)),
            $"Query of view '{view}' on source '{source}' in '{_client.RoutePrefix}'",
            request,
            cancellationToken);

    public Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryApiRequest request,
        CancellationToken cancellationToken)
        where TResult : class =>
        QuerySourceCoreAsync<TResult>(source, request, cancellationToken);

    public Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TResult : class =>
        QuerySourceCoreAsync<TResult>(source, request, cancellationToken);

    private Task<QueryResult<TResult>> QuerySourceCoreAsync<TResult>(
        string source,
        object request,
        CancellationToken cancellationToken)
        where TResult : class =>
        QueryAsync<TResult>(
            QueryableContractUrls.QuerySourceQuery(_client.RoutePrefix, Segment(source)),
            $"Query of source '{source}' in '{_client.RoutePrefix}'",
            request,
            cancellationToken);

    private Task<QueryResult<TResult>> QueryAsync<TResult>(string url, string description, object request, CancellationToken cancellationToken)
        where TResult : class =>
        transport.SendAsync<QueryResult<TResult>>(
            new KaleidoClientRequest(_client, HttpMethod.Post, url, description, request) { InvalidatesRegistryOnNotFound = true },
            cancellationToken);

    // The route segment the service publishes for a source or view: the lower-case name, kept one
    // segment whatever the caller passes.
    private static string Segment(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Uri.EscapeDataString(name.ToLowerInvariant());
    }
}
