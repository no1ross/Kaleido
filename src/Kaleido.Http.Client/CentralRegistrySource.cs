using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

// A participant's full registry (#553): the control registry's, kept in memory and fresh. It holds two
// documents, each with its ETag - the capabilities (they change with a deployment) and each version's
// availability - checked at the intervals the registry hands out with the availability, never the host's
// own; before the first read, and after a failed one, it tries again on RegistryRetry. It composes the one
// KaleidoRegistry every reader on this host gets: each capability with its overall status, never an
// instance. Reads never call out; CentralRegistrySync does. When the registry cannot be read, the last
// good copy is served, partial (a RegistryReadError for the registry) and without a revision, so readers
// rebuild until it recovers.
internal interface ICentralRegistrySource : IKaleidoRegistrySource
{
    // Both documents have been read once: the host's registry is the full registry.
    bool HasRead { get; }

    // How long until the next check: the registry's pace once it is known and reads succeed, else the retry.
    TimeSpan NextCheck { get; }

    // Checks each document that is due (both when forced), and composes the registry when either changed.
    Task SyncAsync(bool force, CancellationToken cancellationToken);
}

internal sealed partial class CentralRegistrySource(
    IHttpClientFactory httpClientFactory,
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
    private int _failedReads;
    private KaleidoRegistry _composed = Unread("The control registry has not been read yet.");

    public bool IsFullRegistry => true;

    public bool HasRead => _capabilities is not null && _availability is not null;

    public TimeSpan NextCheck
    {
        get
        {
            if (_failure is null
                && _availability is { } availability && Pace(x => x.AvailabilityInterval) is { } availabilityEvery
                && _capabilities is { } capabilities && Pace(x => x.CapabilitiesInterval) is { } capabilitiesEvery)
            {
                var next = Min(availability.CheckedUtc + availabilityEvery, capabilities.CheckedUtc + capabilitiesEvery) - time.GetUtcNow();

                return next > TimeSpan.Zero ? next : TimeSpan.Zero;
            }

            return RegistryRetry.After(_failedReads - 1);
        }
    }

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

            if (force || Due(_availability, Pace(x => x.AvailabilityInterval), now))
            {
                (_availability, failure) = await FetchAsync(ControlRegistryContractUrls.Availability, _availability, now, cancellationToken);
            }

            if (force || Due(_capabilities, Pace(x => x.CapabilitiesInterval), now))
            {
                var (capabilities, capabilitiesFailure) = await FetchAsync(ControlRegistryContractUrls.Capabilities, _capabilities, now, cancellationToken);
                _capabilities = capabilities;
                failure ??= capabilitiesFailure;
            }

            _failure = failure;
            _failedReads = failure is null ? 0 : _failedReads + 1;
            _composed = Compose();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Due when never read, when the registry has not named its pace, or once its interval has passed.
    private static bool Due<T>(Fetched<T>? fetched, TimeSpan? interval, DateTimeOffset now) =>
        fetched is null || interval is not { } every || now - fetched.CheckedUtc >= every;

    // An interval the registry handed out with the availability; none before it was read (or if it named none).
    private TimeSpan? Pace(Func<KaleidoRegistryAvailability, TimeSpan> interval) =>
        _availability?.Value is { } availability && interval(availability) is var every && every > TimeSpan.Zero ? every : null;

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

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
