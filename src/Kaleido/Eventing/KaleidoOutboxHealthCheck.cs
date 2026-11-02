using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Eventing;

// The outbox health check of every service (#552): its events wait in the outbox until a relay - on this
// host or another on the same database - publishes them. It reads only that database (the relays'
// heartbeats and the due entries), never calls another service, so it cannot recurse.
internal sealed class KaleidoOutboxHealthCheck(
    IKaleidoOutboxStore store,
    KaleidoServiceOptions serviceOptions,
    TimeProvider time)
    : IHealthCheck
{
    internal const string Name = "kaleido-outbox";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = serviceOptions.Events;
        var now = time.GetUtcNow();
        var status = await store.GetStatusAsync(now, cancellationToken);
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["dueCount"] = status.DueCount,
            ["lastRelaySeenUtc"] = status.LastRelaySeenUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "never",
            ["oldestDueUtc"] = status.OldestDueUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "none",
        };

        var problem = Problem(status, now, options);

        return problem is null
            ? HealthCheckResult.Healthy("A relay drains the outbox.", data)
            : new HealthCheckResult(options.OutboxHealthFailureStatus, problem, data: data);
    }

    private static string? Problem(KaleidoOutboxStatus status, DateTimeOffset now, KaleidoEventOptions options) =>
        status.LastRelaySeenUtc switch
        {
            null => "No relay has ever drained this outbox: the events are not published, and the outbox keeps growing. Add AddOutboxRelay<TPublisher>() to one host on this database.",
            { } seen when now - seen > options.RelaySeenWithin => $"No relay was seen within {options.RelaySeenWithin} (last at {seen:O}): the events are not published, and the outbox keeps growing.",
            _ when status.DueCount > options.BacklogMaxCount => string.Create(CultureInfo.InvariantCulture, $"{status.DueCount} events are due, more than {options.BacklogMaxCount}: the relay does not keep up, or its publisher keeps failing."),
            _ when status.OldestDueUtc is { } oldest && now - oldest > options.BacklogMaxAge => $"The oldest due event occurred at {oldest:O}, longer ago than {options.BacklogMaxAge}: the relay does not keep up, or its publisher keeps failing.",
            _ => null,
        };
}
