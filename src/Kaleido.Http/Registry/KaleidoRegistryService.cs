using Kaleido.Http.Authorization;
using Kaleido.Processor;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Registry;

/// <summary>
/// The unified registry as the current caller may see it, in process: this service's own
/// registrations plus, on an aggregating host, every downstream client's, built once into a
/// shared snapshot and filtered per caller. <c>GET /{service}/registry</c> serves this, and
/// in-host consumers (such as the MCP adapter) call it directly instead of calling their own host
/// over HTTP.
/// </summary>
public interface IKaleidoRegistryService
{
    /// <summary>
    /// True once <c>MapKaleidoHttp()</c> has mapped the registry (and with it decided whether this
    /// host aggregates downstream services). Before that, only local registrations are known.
    /// </summary>
    bool IsMapped { get; }

    /// <summary>
    /// Returns the registry the current caller may see, with its <c>Revision</c> (a hash of the
    /// filtered payload, stable for the same content).
    /// </summary>
    /// <param name="refresh">
    /// Rebuild instead of serving the snapshot (throttled by the refresh cooldown); downstream
    /// client snapshots are invalidated first so the rebuild re-fetches.
    /// </param>
    /// <param name="cancellationToken">Cancels the build.</param>
    Task<AggregatedRegistryResponse> GetForCallerAsync(
        bool refresh = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the registry (local + downstream when aggregating), serves it from the shared snapshot,
/// and filters it for the caller.
/// </summary>
internal sealed class KaleidoRegistryService(
    RegistrySnapshot snapshot,
    LocalRegistrations local,
    IKaleidoDownstreamRegistries downstream,
    IKaleidoAuthorizer authorizer,
    IHttpContextAccessor httpContextAccessor,
    ILogger<KaleidoRegistryService> logger)
    : IKaleidoRegistryService
{
    public bool IsMapped => snapshot.IsMapped;

    public async Task<AggregatedRegistryResponse> GetForCallerAsync(
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var response = await snapshot.GetOrBuildAsync(
            refresh,
            ct => BuildAsync(refresh, ct),
            cancellationToken);

        // Scope the aggregate to the inbound caller's persona — capabilities the caller may not
        // invoke are omitted. The snapshot holds the unfiltered union; filtering is per-request.
        if (authorizer.IsEnforced)
        {
            // "For the caller" whoever asks: Kaleido's endpoints have set the caller already;
            // in-host consumers reached through other endpoints (e.g. MCP) have not.
            httpContextAccessor.HttpContext?.InitializeKaleidoCaller();

            response = await FilterForCallerAsync(response, cancellationToken);
        }

        // Freshness contract: Revision hashes the per-caller filtered payload (same content →
        // same revision), so it is valid as an ETag per caller persona.
        return response with { Revision = ComputeRevision(response) };
    }

    private async Task<AggregatedRegistryResponse> BuildAsync(
        bool refresh,
        CancellationToken cancellationToken)
    {
        // Honored refreshes invalidate downstream client caches first — the rebuild re-fetches,
        // not replays stale per-client snapshots.
        if (refresh && snapshot.Aggregate)
        {
            downstream.Invalidate();
        }

        var remote = snapshot.Aggregate
            ? await downstream.GetAsync(snapshot.DownstreamMaxParallelism, cancellationToken)
            : DownstreamRegistries.None;

        var processes = local.Processes()
            .Concat(remote.Processes)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entryProcessors = processes
            .Where(p => p.IsEntryProcessor)
            .ToArray();

        if (entryProcessors.Length > 1)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"Multiple processors are marked as entry processors: {string.Join(", ", entryProcessors.Select(p => p.Name))}. " +
                "Only one processor in a distributed system should have IsEntryProcessor set to true.");
        }

        var result = new AggregatedRegistryResponse
        {
            Processes = processes,
            Queryables = local.Queryables()
                .Concat(remote.Queryables)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ClientErrors = remote.Errors,
        };

        if (refresh)
        {
            logger.LogDebug("Registry rebuilt via force refresh.");
        }

        if (result.ClientErrors.Count > 0)
        {
            logger.LogWarning(
                "Registry response is partial — {ErrorCount} downstream client(s) failed: {ClientNames}.",
                result.ClientErrors.Count,
                string.Join(", ", result.ClientErrors.Select(e => e.ClientName)));
        }
        else
        {
            logger.LogDebug(
                "Registry response built: {ProcessCount} process(es), {QueryableCount} queryable(s).",
                result.Processes.Count,
                result.Queryables.Count);
        }

        return result;
    }

    private async Task<AggregatedRegistryResponse> FilterForCallerAsync(
        AggregatedRegistryResponse response,
        CancellationToken cancellationToken)
    {
        var processes = new List<ProcessorRegistryResponse>();

        foreach (var processor in response.Processes)
        {
            var initialSteps =
                await authorizer.FilterAsync(processor.InitialSteps,
                    x => x.Authorization,
                    cancellationToken);

            var steps =
                processor.Steps is null
                    ? null
                    : await authorizer.FilterAsync(processor.Steps,
                        x => x.Authorization,
                        cancellationToken);

            // A processor whose every step was filtered out is invisible to the caller — don't
            // advertise an empty shell.
            var hadSteps =
                processor.InitialSteps.Count > 0 || processor.Steps?.Count > 0;

            var hasSteps =
                initialSteps.Count > 0 || steps?.Count > 0;

            if (hadSteps && !hasSteps)
            {
                continue;
            }

            processes.Add(
                processor with
                {
                    InitialSteps = initialSteps,
                    Steps = steps,
                });
        }

        var queryables = new List<QueryableSourceResponse>();

        foreach (var queryable in response.Queryables)
        {
            var sourceAllowed =
                await authorizer.CanAccessAsync(queryable.Authorization, cancellationToken);

            var views =
                await authorizer.FilterAsync(queryable.Views,
                    x => x.Authorization,
                    cancellationToken);

            if (!sourceAllowed && views.Count == 0)
            {
                continue;
            }

            queryables.Add(
                queryable with
                {
                    QueryUrl = sourceAllowed ? queryable.QueryUrl : null,
                    Views = views,
                });
        }

        return response with
        {
            Processes = processes,
            Queryables = queryables,
        };
    }

    // SHA-256 over the filtered payload — same content always produces the same revision.
    private static string ComputeRevision(AggregatedRegistryResponse response)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            response with { Revision = null },
            KaleidoJsonOptions.Options);

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }
}
