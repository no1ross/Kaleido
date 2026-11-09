using Kaleido.Processor;
using Kaleido.Server.Authorization;
using Kaleido.Server.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Server.Registry;

/// <summary>
/// The central registry as the current caller may see it, in process: its full hydrated copy
/// filtered per caller. In-host consumers such as the MCP adapter read that copy without calling
/// their own host over HTTP.
/// </summary>
public interface IKaleidoRegistryService
{
    /// <summary>
    /// Returns the registry the current caller may see, with its <c>Revision</c> (a hash of the
    /// filtered payload, stable for the same content).
    /// </summary>
    /// <param name="refresh">
    /// Refresh the central registry source before reading its full hydrated copy.
    /// </param>
    /// <param name="cancellationToken">Cancels the build.</param>
    Task<KaleidoRegistry> GetForCallerAsync(
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
    /// <summary>The central registry's hydrated copy, unfiltered, with its <c>Revision</c>.</summary>
    Task<KaleidoRegistry> GetUnfilteredAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Filters the central registry's hydrated copy for the current caller.
/// </summary>
internal sealed partial class KaleidoRegistryService(
    IKaleidoRegistrySource source,
    IKaleidoAuthorizer authorizer,
    IHttpContextAccessor httpContextAccessor,
    ILogger<KaleidoRegistryService> logger)
    : IKaleidoRegistryService, IKaleidoUnfilteredRegistry
{
    // The snapshot carries its revision, computed once when it was built (#553): readers are served it
    // as it is, however many check it.
    public Task<KaleidoRegistry> GetUnfilteredAsync(CancellationToken cancellationToken) =>
        SnapshotAsync(refresh: false, cancellationToken);

    public async Task<KaleidoRegistry> GetForCallerAsync(
        bool refresh,
        CancellationToken cancellationToken)
    {
        var response = await SnapshotAsync(refresh, cancellationToken);

        // Scope the aggregate to the inbound caller's persona — capabilities the caller may not
        // invoke are omitted. The snapshot holds the unfiltered union; filtering is per-request.
        if (!authorizer.IsEnforced)
        {
            return response;
        }

        // "For the caller" whoever asks: Kaleido's endpoints have set the caller already;
        // in-host consumers reached through other endpoints (e.g. MCP) have not.
        httpContextAccessor.HttpContext?.InitializeKaleidoCaller();

        response = await FilterForCallerAsync(response, cancellationToken);

        // Freshness contract: Revision hashes the per-caller filtered payload (same content →
        // same revision), so it is valid as an ETag per caller persona.
        return response with { Revision = ComputeRevision(response) };
    }

    // A full registry (the control registry's, or a participant's copy of it, #553) is kept by its source in
    // memory, with a revision: readers take the latest hydrated copy, without a per-service snapshot or
    // downstream fan-out. A failed read keeps the source's last good copy, marked partial.
    private async Task<KaleidoRegistry> SnapshotAsync(bool refresh, CancellationToken cancellationToken)
    {
        if (refresh)
        {
            await source.RefreshAsync(cancellationToken);
            LogRegistryRebuilt(logger);
        }

        var full = await source.GetAsync(cancellationToken);
        EnsureOneEntryProcessor([.. full.Processes]);
        var result = full with { IsPartial = full.Errors.Count > 0 || full.Missing.Count > 0 };

        if (result.IsPartial)
        {
            LogRegistryPartial(logger, result.Errors.Count + result.Missing.Count, string.Join(", ", result.Errors.Select(e => e.Source).Concat(result.Missing.Select(m => m.ServiceName))));
        }
        else
        {
            LogRegistryBuilt(logger, result.Processes.Count, result.Queryables.Count);
        }

        return result;
    }

    // Versions of one service are one processor here: each may be marked (#553).
    private static void EnsureOneEntryProcessor(ProcessorRegistryResponse[] processes)
    {
        var entryProcessors = processes
            .Where(p => p.IsEntryProcessor)
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entryProcessors.Length > 1)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"Multiple processors are marked as entry processors: {string.Join(", ", entryProcessors.Select(p => p.Name))}. " +
                "Only one processor in a distributed system should have IsEntryProcessor set to true.");
        }
    }

    private async Task<KaleidoRegistry> FilterForCallerAsync(
        KaleidoRegistry response,
        CancellationToken cancellationToken)
    {
        var lookups = new LookupSources(response.Queryables);
        var processes = await FilterProcessesAsync(response.Processes, lookups, cancellationToken);
        var queryables = await FilterQueryablesAsync(response.Queryables, cancellationToken);
        static string Key(string service, string? version) => $"{service.ToLowerInvariant()}/{version}";
        var visible = processes.Select(x => Key(x.ServiceName, x.ServiceVersion))
            .Concat(queryables.Select(x => Key(x.ServiceName, x.ServiceVersion)))
            .ToHashSet(StringComparer.Ordinal);

        return response with
        {
            Processes = processes,
            Queryables = queryables,
            Routes = [.. response.Routes.Where(x => visible.Contains(Key(x.ServiceName, x.ServiceVersion)))],
        };
    }

    private async Task<List<ProcessorRegistryResponse>> FilterProcessesAsync(
        IEnumerable<ProcessorRegistryResponse> processors,
        LookupSources lookups,
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
                    Steps = steps is null ? null : await DescribeForCallerAsync(steps, lookups, cancellationToken),
                });
        }

        return processes;
    }

    // A caller is never described a step or a query they may not use (#524): a kept step's
    // relationships keep their shape with only the steps the caller may run, and an input whose
    // lookup names a query the caller may not use keeps the input without the lookup, with a notice.
    private async Task<List<ProcessStepResponse>> DescribeForCallerAsync(
        IReadOnlyList<ProcessStepResponse> steps,
        LookupSources lookups,
        CancellationToken cancellationToken)
    {
        var described = new List<ProcessStepResponse>(steps.Count);

        foreach (var step in steps)
        {
            var fields = new List<ProcessFieldMetadata>(step.Fields.Count);

            foreach (var field in step.Fields)
            {
                fields.Add(field.Lookup is { } lookup && !await CanUseAsync(lookup, lookups, cancellationToken)
                    ? NotPermittedLookup(step, field, lookup)
                    : field);
            }

            described.Add(step with
            {
                Dependencies = await authorizer.FilterAsync(step.Dependencies, x => x.Authorization, cancellationToken),
                AvailableAfter = await authorizer.FilterAsync(step.AvailableAfter, x => x.Authorization, cancellationToken),
                AvailableUntil = await authorizer.FilterAsync(step.AvailableUntil, x => x.Authorization, cancellationToken),
                Fields = fields,
            });
        }

        return described;
    }

    private ProcessFieldMetadata NotPermittedLookup(ProcessStepResponse step, ProcessFieldMetadata field, ProcessFieldLookup lookup)
    {
        LogLookupNotPermitted(logger, step.Name, field.Name, lookup.Service, lookup.Source, lookup.View);

        return field with { Lookup = null, Notice = LookupNotPermitted };
    }

    internal static readonly KaleidoNotice LookupNotPermitted =
        new(ProcessorErrorCodes.NotPermitted, "You do not have access to the query that fills this input.");

    // The lookup's query as the caller may use it, by the rules the full registry publishes for it,
    // evaluated here for the caller as a router does. A query found nowhere is not permitted. Hosts that
    // check each other's lookups share policy names.
    private async Task<bool> CanUseAsync(ProcessFieldLookup lookup, LookupSources lookups, CancellationToken cancellationToken)
    {
        var queried = await lookups.FindAsync(lookup, cancellationToken);

        if (queried is null)
        {
            return false;
        }

        if (lookup.View is not { } viewName)
        {
            return await authorizer.CanAccessAsync(queried.Authorization, cancellationToken);
        }

        var view = queried.Views.FirstOrDefault(x => string.Equals(x.Name, viewName, StringComparison.OrdinalIgnoreCase));

        return view is not null && await authorizer.CanAccessAsync(view.Authorization, cancellationToken);
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
    private static string ComputeRevision(KaleidoRegistry response)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            response with { Revision = null, GeneratedAt = default },
            KaleidoJsonOptions.Options);

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Step {StepName} input {FieldName} looks up {Service}/{Source}/{View}, which the caller may not use (or which is not published to this host); the lookup is left out (pro_not_permitted). Check the caller's roles, and that the hosts share policy names.")]
    private static partial void LogLookupNotPermitted(ILogger logger, string stepName, string fieldName, string service, string source, string? view);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registry rebuilt via force refresh.")]
    private static partial void LogRegistryRebuilt(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry response is partial — {ErrorCount} downstream client(s) failed or expected service(s) are missing: {ClientNames}.")]
    private static partial void LogRegistryPartial(ILogger logger, int errorCount, string clientNames);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registry response built: {ProcessCount} process(es), {QueryableCount} queryable(s).")]
    private static partial void LogRegistryBuilt(ILogger logger, int processCount, int queryableCount);
}
