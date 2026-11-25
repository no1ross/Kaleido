using System.Net.Http.Json;

namespace Kaleido.Queryable.Http.Client;

// A service's queries resolve their published source and view routes from the unfiltered central registry.
internal sealed class KaleidoQueryableClient(
    string serviceName,
    string? serviceVersion,
    IKaleidoRegistryTargetResolver targets,
    IHttpClientFactory httpClientFactory,
    IKaleidoInboundCallerAccessor inboundCaller,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoServiceOptions localServiceOptions)
    : IKaleidoQueryableClient
{
    internal const string ClientName = "kaleido-queryable";

    public async Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TView : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        ArgumentNullException.ThrowIfNull(request);
        var (published, route) = await SourceAsync(source, cancellationToken);
        var selected = published.Views.FirstOrDefault(x => string.Equals(x.Name, view, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry does not publish view '{view}' on source '{source}' in service '{serviceName}'.");
        return await QueryAsync<TView>(QueryPath(selected.Bindings, route),
            $"Query of view '{view}' on source '{source}' in '{serviceName}'",
            new QueryApiRequest<TParameters>(request.ViewParameters, request.Query.ToApiBody()), cancellationToken);
    }

    public Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryRequest request,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(request);
        return QuerySourceCoreAsync<TResult>(source, new QueryApiRequest(request.Query.ToApiBody()), cancellationToken);
    }

    public Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(request);
        return QuerySourceCoreAsync<TResult>(source, new QueryApiRequest<TParameters>(request.ViewParameters, request.Query.ToApiBody()), cancellationToken);
    }

    private async Task<QueryResult<TResult>> QuerySourceCoreAsync<TResult>(string source, object request, CancellationToken cancellationToken)
        where TResult : class
    {
        var (published, route) = await SourceAsync(source, cancellationToken);
        return await QueryAsync<TResult>(QueryPath(published.Bindings, route),
            $"Query of source '{source}' in '{serviceName}'", request, cancellationToken);
    }

    private static string QueryPath(RegistryTransportBindings? bindings, RegistryTransportRoute route)
    {
        var operations = bindings?.Http?.Where(x => string.Equals(x?.Operation, "query", StringComparison.OrdinalIgnoreCase)
                && string.Equals(x?.Method, "POST", StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray() ?? [];
        return operations.Length == 1
            ? QueryablePublishedHttpRoute.RequirePath(operations[0].Path, route.GatewayRoute)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                "The central registry does not publish one POST query operation for this capability.");
    }

    private async Task<(QueryableRegistryItem Source, RegistryTransportRoute Route)> SourceAsync(string source, CancellationToken cancellationToken)
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

    private async Task<QueryResult<TResult>> QueryAsync<TResult>(string url, string description, object body, CancellationToken cancellationToken)
        where TResult : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url.TrimStart('/'))
        {
            Content = JsonContent.Create(body, body.GetType(), options: KaleidoJsonOptions.Options),
        };
        if (inboundCaller.IsInboundRequest)
        {
            request.Options.Set(KaleidoCallContext.Key, KaleidoCallContext.ForCaller(inboundCaller.Principal, inboundCaller.Credential));
        }
        else
        {
            KaleidoCallContext.MarkServiceOwn(request);
        }

        request.StampKaleidoCorrelation(correlation.Current, localServiceName: localServiceOptions.ServiceName);
        using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.ToClientFailureAsync($"{description} failed", cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<QueryResult<TResult>>(KaleidoJsonOptions.Options, cancellationToken)
            ?? throw new KaleidoHttpClientException(HttpClientErrorCodes.EmptyResponse,
                $"{description} succeeded but returned no payload.", response.StatusCode);
    }
}
