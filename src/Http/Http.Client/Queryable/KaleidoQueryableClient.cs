namespace Kaleido.Http.Client.Queryable;

// A service's queries resolve their published source and view routes from the unfiltered central registry.
internal sealed class KaleidoQueryableClient(
    string serviceName,
    string? serviceVersion,
    IKaleidoRegistryTargetResolver targets,
    IKaleidoClientTransport transport)
    : IKaleidoQueryableClient
{
    public async Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TView : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        var (published, route) = await SourceAsync(source, cancellationToken);
        var selected = published.Views.FirstOrDefault(x => string.Equals(x.Name, view, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry does not publish view '{view}' on source '{source}' in service '{serviceName}'.");
        return await QueryAsync<TView>(KaleidoPublishedHttpRoute.RequirePath(selected.QueryUrl, route.GatewayRoute),
            $"Query of view '{view}' on source '{source}' in '{serviceName}'", request, cancellationToken);
    }

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

    private async Task<QueryResult<TResult>> QuerySourceCoreAsync<TResult>(string source, object request, CancellationToken cancellationToken)
        where TResult : class
    {
        var (published, route) = await SourceAsync(source, cancellationToken);
        return await QueryAsync<TResult>(KaleidoPublishedHttpRoute.RequirePath(published.QueryUrl, route.GatewayRoute),
            $"Query of source '{source}' in '{serviceName}'", request, cancellationToken);
    }

    private Task<QueryResult<TResult>> QueryAsync<TResult>(string url, string description, object request, CancellationToken cancellationToken)
        where TResult : class =>
        transport.SendAsync<QueryResult<TResult>>(
            new KaleidoClientRequest(serviceName, HttpMethod.Post, url, description, request), cancellationToken);

    private async Task<(QueryableSourceResponse Source, RegistryTransportRoute Route)> SourceAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var target = await targets.ResolveAsync(serviceName, serviceVersion, ["http"], cancellationToken);
        var published = target.Registry.Queryables.FirstOrDefault(x =>
            string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ServiceVersion, target.Service.ServiceVersion, StringComparison.Ordinal)
            && string.Equals(x.Name, source, StringComparison.OrdinalIgnoreCase));
        return published is not null
            ? (published, target.Transport)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry does not publish source '{source}' on service '{serviceName}' version '{target.Service.ServiceVersion}'.");
    }
}
