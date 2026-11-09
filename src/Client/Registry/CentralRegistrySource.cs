using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Client.Registry;

// A participant's full registry (#553): the control registry's, kept in memory and fresh. It holds two
// documents, each with its ETag - each version's availability, checked at the interval the registry hands
// out with it (never the host's own), and the capabilities, read exactly when the availability names other
// capabilities than those held (a deployment); before the first read, and after a failed one, it tries
// again on RegistryRetry. It composes the one
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
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<CentralRegistrySource> logger)
    : ICentralRegistrySource, IDisposable
{
    internal const string ErrorSource = "registry";

    private static readonly KaleidoRegistryAvailability NoAvailability = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Fetched<KaleidoRegistry>? _capabilities;
    private Fetched<KaleidoRegistryAvailability>? _availability;
    private string? _failure;
    private int _failedReads;
    private KaleidoRegistry _composed = Unread("The control registry has not been read yet.");

    public bool HasRead => _capabilities is not null && _availability is not null;

    public TimeSpan NextCheck
    {
        get
        {
            if (_failure is null && _capabilities is not null && _availability is { } availability && Pace(availability.Value) is { } every)
            {
                var next = availability.CheckedUtc + every - time.GetUtcNow();

                return next > TimeSpan.Zero ? next : TimeSpan.Zero;
            }

            return RegistryRetry.After(_failedReads - 1);
        }
    }

    public Task<KaleidoRegistry> GetAsync(CancellationToken cancellationToken) =>
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

            if (force || _availability is null || _availability.Value is not { } held || Pace(held) is not { } every || now - _availability.CheckedUtc >= every)
            {
                (_availability, failure) = await FetchAsync((client, etag, ct) => client.GetAvailabilityAsync(etag, ct), "availability", _availability, now, cancellationToken);
            }

            if (force || Stale(_capabilities, _availability?.Value))
            {
                var (capabilities, capabilitiesFailure) = await FetchAsync((client, etag, ct) => client.GetCapabilitiesAsync(etag, ct), "capabilities", _capabilities, now, cancellationToken);
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

    // The capabilities are read when never read, or when the availability names others than those held (a
    // deployment); an availability naming none has them checked each time, with their ETag.
    private static bool Stale(Fetched<KaleidoRegistry>? capabilities, KaleidoRegistryAvailability? availability) =>
        capabilities is null
        || availability?.CapabilitiesRevision is not { } current
        || !string.Equals(current, capabilities.Value.Revision, StringComparison.Ordinal);

    // The interval the registry handed out with the availability; none if it named none.
    private static TimeSpan? Pace(KaleidoRegistryAvailability availability) =>
        availability.AvailabilityInterval > TimeSpan.Zero ? availability.AvailabilityInterval : null;

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
    private async Task<(Fetched<T>? Fetched, string? Failure)> FetchAsync<T>(
        Func<IKaleidoRegistryClient, string?, CancellationToken, Task<KaleidoRegistryDocument<T>>> fetch,
        string document,
        Fetched<T>? held,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>();
            var result = await fetch(client, held?.ETag, cancellationToken);

            if (result.NotModified)
            {
                return held is not null
                    ? (held with { CheckedUtc = now }, null)
                    : (null, $"The control registry answered {document} unchanged before it was read.");
            }

            return result.Value is { } value
                ? (new Fetched<T>(value, result.ETag, now), null)
                : (held, $"The control registry answered {document} with no payload.");
        }
        catch (KaleidoRegistryClientException exception)
        {
            if (exception.Status is { } status)
            {
                LogReadFailed(logger, document, status);
                return (held, $"The control registry answered {status} to {document}.");
            }

            LogReadError(logger, document, exception);
            return (held, $"The control registry could not be read ({document}): {exception.Message}");
        }
    }

    private sealed record Fetched<T>(T Value, string? ETag, DateTimeOffset CheckedUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry answered {StatusCode} to {Document}; this host keeps its last copy of the registry.")]
    private static partial void LogReadFailed(ILogger logger, string document, string? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry could not be read ({Document}); this host keeps its last copy of the registry.")]
    private static partial void LogReadError(ILogger logger, string document, Exception exception);
}
