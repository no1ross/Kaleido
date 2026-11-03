using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

// A participant's full registry (#553): the control registry's, kept in memory and fresh. It holds two
// documents, each with its ETag - the capabilities, checked every CapabilitiesInterval (they change with a
// deployment), and each version's availability, checked every AvailabilityInterval - and composes the one
// KaleidoRegistry every reader on this host gets: each capability with its overall status, never an
// instance. Reads never call out; CentralRegistrySync does. When the registry cannot be read, the last
// good copy is served, partial (a RegistryReadError for the registry) and without a revision, so readers
// rebuild until it recovers.
internal interface ICentralRegistrySource : IKaleidoRegistrySource
{
    // Both documents have been read once: the host's registry is the full registry.
    bool HasRead { get; }

    // Checks each document that is due (both when forced), and composes the registry when either changed.
    Task SyncAsync(bool force, CancellationToken cancellationToken);
}

internal sealed partial class CentralRegistrySource(
    IHttpClientFactory httpClientFactory,
    KaleidoRegistryClientOptions options,
    TimeProvider time,
    ILogger<CentralRegistrySource> logger)
    : ICentralRegistrySource, IDisposable
{
    internal const string ClientName = "kaleido-registry";
    internal const string ErrorSource = "registry";

    private static readonly KaleidoRegistryAvailability NoAvailability = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Fetched<KaleidoRegistry>? _capabilities;
    private Fetched<KaleidoRegistryAvailability>? _availability;
    private string? _failure;
    private KaleidoRegistry _composed = Unread("The control registry has not been read yet.");

    public bool IsFullRegistry => true;

    public bool HasRead => _capabilities is not null && _availability is not null;

    public Task<KaleidoRegistry> GetAsync(int? maxParallelism, CancellationToken cancellationToken) =>
        Task.FromResult(_composed);

    public Task<KaleidoRegistryAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_availability?.Value ?? NoAvailability);

    // Reads both documents now.
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        SyncAsync(force: true, cancellationToken);

    public void Dispose() => _gate.Dispose();

    public async Task SyncAsync(bool force, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            var failure = default(string);

            if (force || Due(_capabilities, options.CapabilitiesInterval, now))
            {
                (_capabilities, failure) = await FetchAsync(ControlRegistryContractUrls.Capabilities, _capabilities, now, cancellationToken);
            }

            if (force || Due(_availability, options.AvailabilityInterval, now))
            {
                var (availability, availabilityFailure) = await FetchAsync(ControlRegistryContractUrls.Availability, _availability, now, cancellationToken);
                _availability = availability;
                failure ??= availabilityFailure;
            }

            _failure = failure;
            _composed = Compose();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool Due<T>(Fetched<T>? fetched, TimeSpan interval, DateTimeOffset now) =>
        fetched is null || now - fetched.CheckedUtc >= interval;

    private KaleidoRegistry Compose()
    {
        if (_capabilities is null)
        {
            return Unread(_failure ?? "The control registry has not been read yet.");
        }

        var registry = _capabilities.Value.WithAvailability(_availability?.Value ?? NoAvailability);

        return _failure is null
            ? registry
            : registry with { Errors = [new RegistryReadError { Source = ErrorSource, Reason = _failure }], Revision = null };
    }

    private static KaleidoRegistry Unread(string reason) =>
        new() { Errors = [new RegistryReadError { Source = ErrorSource, Reason = reason }] };

    // A conditional read: 304 keeps what is held; a failure keeps it too, with the reason.
    private async Task<(Fetched<T>? Fetched, string? Failure)> FetchAsync<T>(string url, Fetched<T>? held, DateTimeOffset now, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url.TrimStart('/'));
            KaleidoCallContext.MarkServiceOwn(request);

            if (held?.ETag is { } etag)
            {
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
            }

            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);

            if (held is not null && response.StatusCode == HttpStatusCode.NotModified)
            {
                return (held with { CheckedUtc = now }, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogReadFailed(logger, url, (int)response.StatusCode);
                return (held, string.Create(CultureInfo.InvariantCulture, $"The control registry answered {(int)response.StatusCode} to {url}."));
            }

            return await response.Content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, cancellationToken) is { } value
                ? (new Fetched<T>(value, response.Headers.ETag?.ToString(), now), null)
                : (held, $"The control registry answered {url} with no payload.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !cancellationToken.IsCancellationRequested)
        {
            LogReadError(logger, url, exception);
            return (held, $"The control registry could not be read ({url}): {exception.Message}");
        }
    }

    private sealed record Fetched<T>(T Value, string? ETag, DateTimeOffset CheckedUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry answered {StatusCode} to {Url}; this host keeps its last copy of the registry.")]
    private static partial void LogReadFailed(ILogger logger, string url, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry could not be read ({Url}); this host keeps its last copy of the registry.")]
    private static partial void LogReadError(ILogger logger, string url, Exception exception);
}
