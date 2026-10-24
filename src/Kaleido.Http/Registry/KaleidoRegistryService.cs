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
        bool refresh,
        CancellationToken cancellationToken);
}

/// <summary>
/// The unified registry with nothing filtered out, for in-host consumers whose results must not depend
/// on what one caller may see: the MCP adapter derives stable tool names from it, so a name never moves
/// to another capability when a caller sees fewer of them (#435). Internal: never served to a caller.
/// </summary>
internal interface IKaleidoUnfilteredRegistry
{
    /// <summary>The shared snapshot, unfiltered, with its <c>Revision</c> (a hash of its content).</summary>
    Task<AggregatedRegistryResponse> GetUnfilteredAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Builds the registry (local + downstream when aggregating), serves it from the shared snapshot,
/// and filters it for the caller.
/// </summary>
internal sealed partial class KaleidoRegistryService(
    RegistrySnapshot snapshot,
    LocalRegistrations local,
    IKaleidoDownstreamRegistries downstream,
    IKaleidoAuthorizer authorizer,
    IHttpContextAccessor httpContextAccessor,
    ILogger<KaleidoRegistryService> logger)
    : IKaleidoRegistryService, IKaleidoUnfilteredRegistry
{
    public bool IsMapped => snapshot.IsMapped;

    public async Task<AggregatedRegistryResponse> GetUnfilteredAsync(CancellationToken cancellationToken)
    {
        var response = await snapshot.GetOrBuildAsync(
            refresh: false,
            ct => BuildAsync(refresh: false, ct),
            cancellationToken);

        return response with { Revision = ComputeRevision(response) };
    }

    public async Task<AggregatedRegistryResponse> GetForCallerAsync(
        bool refresh,
        CancellationToken cancellationToken)
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
            await downstream.InvalidateAsync(cancellationToken);
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
            LogRegistryRebuilt(logger);
        }

        if (result.ClientErrors.Count > 0)
        {
            LogRegistryPartial(logger, result.ClientErrors.Count, string.Join(", ", result.ClientErrors.Select(e => e.ClientName)));
        }
        else
        {
            LogRegistryBuilt(logger, result.Processes.Count, result.Queryables.Count);
        }

        return result;
    }

    private async Task<AggregatedRegistryResponse> FilterForCallerAsync(
        AggregatedRegistryResponse response,
        CancellationToken cancellationToken)
    {
        return response with
        {
            Processes = await FilterProcessesAsync(response.Processes, cancellationToken),
            Queryables = await FilterQueryablesAsync(response.Queryables, cancellationToken),
        };
    }

    private async Task<List<ProcessorRegistryResponse>> FilterProcessesAsync(
        IEnumerable<ProcessorRegistryResponse> processors,
        CancellationToken cancellationToken)
    {
        var processes = new List<ProcessorRegistryResponse>();

        foreach (var processor in processors)
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

        return processes;
    }

    // A source the caller may not query directly stays listed (without its URL) while it has
    // views the caller may use.
    private async Task<List<QueryableSourceResponse>> FilterQueryablesAsync(
        IEnumerable<QueryableSourceResponse> sources,
        CancellationToken cancellationToken)
    {
        var queryables = new List<QueryableSourceResponse>();

        foreach (var queryable in sources)
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

        return queryables;
    }

    // SHA-256 over the filtered payload — same content always produces the same revision. The build
    // time is not content: a rebuilt but unchanged registry keeps its revision, so a client checking it
    // with its ETag gets 304.
    private static string ComputeRevision(AggregatedRegistryResponse response)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            response with { Revision = null, GeneratedAt = default },
            KaleidoJsonOptions.Options);

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registry rebuilt via force refresh.")]
    private static partial void LogRegistryRebuilt(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry response is partial — {ErrorCount} downstream client(s) failed: {ClientNames}.")]
    private static partial void LogRegistryPartial(ILogger logger, int errorCount, string clientNames);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registry response built: {ProcessCount} process(es), {QueryableCount} queryable(s).")]
    private static partial void LogRegistryBuilt(ILogger logger, int processCount, int queryableCount);
}
