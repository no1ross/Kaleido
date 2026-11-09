using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Platform;

namespace Kaleido.Http.Client.Registry;

internal sealed class KaleidoHttpRegistryClient(
    IHttpClientFactory httpClientFactory,
    IKaleidoInboundCallerAccessor inboundCaller)
    : IKaleidoRegistryClient
{
    internal const string ClientName = "kaleido-registry";

    public Task<KaleidoRegistryDocument<KaleidoRegistry>> GetRegistryAsync(string? etag, CancellationToken cancellationToken) =>
        ReadAsync<KaleidoRegistry>(ControlRegistryContractUrls.Registry, etag, forCaller: true, cancellationToken);

    public Task<KaleidoRegistryDocument<KaleidoRegistry>> GetCapabilitiesAsync(string? etag, CancellationToken cancellationToken) =>
        ReadAsync<KaleidoRegistry>(ControlRegistryContractUrls.Capabilities, etag, forCaller: false, cancellationToken);

    public Task<KaleidoRegistryDocument<KaleidoRegistryAvailability>> GetAvailabilityAsync(string? etag, CancellationToken cancellationToken) =>
        ReadAsync<KaleidoRegistryAvailability>(ControlRegistryContractUrls.Availability, etag, forCaller: false, cancellationToken);

    public async Task<RegistryLease> RegisterAsync(RegistryInstanceRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        using var request = Own(HttpMethod.Post, ControlRegistryContractUrls.Instances);
        request.Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options);
        using var response = await SendAsync(request, cancellationToken);
        await RequireSuccessAsync(response, cancellationToken);
        return await BodyAsync<RegistryLease>(response, cancellationToken);
    }

    public async Task<RegistryLease?> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        using var request = Own(HttpMethod.Put, ControlRegistryContractUrls.Heartbeat(instanceId));
        request.Content = JsonContent.Create(heartbeat, options: KaleidoJsonOptions.Options);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await RequireSuccessAsync(response, cancellationToken);
        return await BodyAsync<RegistryLease>(response, cancellationToken);
    }

    public async Task DeregisterAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        using var request = Own(HttpMethod.Delete, ControlRegistryContractUrls.Instance(instanceId));
        using var response = await SendAsync(request, cancellationToken);
        await RequireSuccessAsync(response, cancellationToken);
    }

    private async Task<KaleidoRegistryDocument<T>> ReadAsync<T>(string path, string? etag, bool forCaller, CancellationToken cancellationToken)
        where T : class
    {
        using var request = Own(HttpMethod.Get, path);
        if (forCaller && inboundCaller.IsInboundRequest)
        {
            request.Options.Set(KaleidoCallContext.Key, KaleidoCallContext.ForCaller(inboundCaller.Principal, inboundCaller.Credential));
        }

        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified && etag is not null)
        {
            return new(Value: null, ETag: response.Headers.ETag?.ToString() ?? etag, NotModified: true);
        }

        await RequireSuccessAsync(response, cancellationToken);
        return new(Value: await BodyAsync<T>(response, cancellationToken), ETag: response.Headers.ETag?.ToString(), NotModified: false);
    }

    private static HttpRequestMessage Own(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        KaleidoCallContext.MarkServiceOwn(request);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RequestFailed,
                "The central registry could not be reached.", innerException: exception);
        }
    }

    private static async Task RequireSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var failure = await response.ToClientFailureAsync("Central registry request", cancellationToken);
            throw new KaleidoRegistryClientException(
                RegistryClientErrorCodes.RequestFailed,
                failure.Message,
                ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                failure);
        }
    }

    private static async Task<T> BodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.EmptyResponse,
                "The central registry answered without a document.", status);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, cancellationToken)
                ?? throw new KaleidoRegistryClientException(RegistryClientErrorCodes.EmptyResponse,
                    "The central registry answered without a document.", status);
        }
        catch (JsonException exception)
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.InvalidResponse,
                "The central registry answered with an invalid document.", status, exception);
        }
    }
}
