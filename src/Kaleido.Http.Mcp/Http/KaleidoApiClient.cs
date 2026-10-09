using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kaleido.Http.Mcp.Http;

/// <summary>The outcome of one Kaleido HTTP call: the status and the JSON body (if any).</summary>
[ExcludeFromCodeCoverage]
internal sealed record KaleidoApiResponse(
    int StatusCode,
    JsonNode? Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

/// <summary>
/// A registry read: the caller-filtered registry, or <see cref="NotModified"/> when the revision
/// the adapter already holds is still current (the registry answered 304).
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record KaleidoRegistryRead(
    AggregatedRegistryResponse? Registry,
    bool NotModified);

/// <summary>
/// The adapter's only way to reach Kaleido: plain HTTP against the configured base URL with the
/// published contracts. The caller's credentials are forwarded by
/// <see cref="KaleidoForwardingHandler"/>, so Kaleido authorizes the agent's user directly. The
/// adapter needs no shared state with the Kaleido host and can run on another server.
/// </summary>
internal interface IKaleidoApiClient
{
    /// <summary>
    /// Reads the caller-filtered registry. Pass the revision already held to receive
    /// <see cref="KaleidoRegistryRead.NotModified"/> instead of the full registry when unchanged.
    /// </summary>
    Task<KaleidoRegistryRead> GetRegistryAsync(
        string? knownRevision = null,
        CancellationToken cancellationToken = default);

    /// <summary>GETs <paramref name="path"/> (relative to the base URL).</summary>
    Task<KaleidoApiResponse> GetAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POSTs <paramref name="body"/> to <paramref name="path"/>. When <paramref name="processId"/>
    /// is set it is sent in <c>X-Kaleido-Process-Id</c> (continue an existing process).
    /// </summary>
    Task<KaleidoApiResponse> PostAsync(
        string path,
        JsonNode body,
        Guid? processId = null,
        CancellationToken cancellationToken = default);
}

internal sealed class KaleidoApiClient(
    IHttpClientFactory httpClientFactory,
    KaleidoMcpOptions options)
    : IKaleidoApiClient
{
    /// <summary>The named <see cref="HttpClient"/> the adapter uses (base address = BaseUrl).</summary>
    public const string HttpClientName = "Kaleido.Http.Mcp";

    public async Task<KaleidoRegistryRead> GetRegistryAsync(
        string? knownRevision = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request =
            new HttpRequestMessage(HttpMethod.Get, new Uri(options.RegistryPath, UriKind.Relative));

        if (!string.IsNullOrWhiteSpace(knownRevision))
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{knownRevision}\""));
        }

        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new KaleidoRegistryRead(null, NotModified: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new KaleidoFrameworkException(
                McpErrorCodes.ServiceError,
                $"Reading the Kaleido registry at '{options.RegistryPath}' returned HTTP {(int)response.StatusCode}.");
        }

        var registry =
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(
                KaleidoJsonOptions.Options,
                cancellationToken);

        return new KaleidoRegistryRead(registry, NotModified: false);
    }

    public async Task<KaleidoApiResponse> GetAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var response =
            await client.GetAsync(
                new Uri(path, UriKind.Relative),
                cancellationToken);

        return await ReadAsync(response, cancellationToken);
    }

    public async Task<KaleidoApiResponse> PostAsync(
        string path,
        JsonNode body,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request =
            new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
            {
                Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options)
            };

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        using var response = await client.SendAsync(request, cancellationToken);

        return await ReadAsync(response, cancellationToken);
    }

    private static async Task<KaleidoApiResponse> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonNode? body = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                body = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                // Not JSON (e.g. a proxy error page): keep the text so the agent can report it.
                body = JsonValue.Create(text);
            }
        }

        return new KaleidoApiResponse((int)response.StatusCode, body);
    }
}
