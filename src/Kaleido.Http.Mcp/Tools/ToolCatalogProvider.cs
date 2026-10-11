using System.Collections.Concurrent;
using Kaleido.Http.Mcp.Observability;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Gives the current caller their tools: the host's registry as this caller may see it
/// (<see cref="IKaleidoRegistryService"/> — the same shared snapshot and per-caller filtering as
/// <c>GET /{service}/registry</c>), translated into MCP tools.
/// </summary>
/// <remarks>
/// Nothing is cached per caller. The translation is reused per registry revision: the revision
/// hashes the caller-filtered registry, so the same revision always yields the same tools.
/// </remarks>
internal interface IToolCatalogProvider
{
    Task<ToolCatalog> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed partial class ToolCatalogProvider(
    IKaleidoRegistryService registry,
    IToolCatalogBuilder catalogBuilder,
    IMcpObservability observability,
    ILogger<ToolCatalogProvider> logger)
    : IToolCatalogProvider
{
    private const int MaxRevisions = 256;

    // Translated catalogs by registry revision (one per distinct caller-visible registry).
    private static readonly ConcurrentDictionary<string, ToolCatalog> Catalogs = new(StringComparer.Ordinal);

    public async Task<ToolCatalog> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!registry.IsMapped)
        {
            return new ToolCatalog
            {
                Unavailable = "The host has not mapped its Kaleido registry; call MapKaleidoHttp(o => o.AggregateRegistry = true) before MapKaleidoMcp().",
            };
        }

        var response = await registry.GetForCallerAsync(cancellationToken: cancellationToken);
        var revision = response.Revision ?? string.Empty;

        if (Catalogs.TryGetValue(revision, out var known))
        {
            observability.RegistryRead(McpTelemetry.RegistryHit, revision);

            return known;
        }

        var catalog = catalogBuilder.Build(response);

        if (Catalogs.Count >= MaxRevisions)
        {
            Catalogs.Clear();
        }

        Catalogs[revision] = catalog;
        observability.RegistryRead(McpTelemetry.RegistryFetched, revision);
        Report(catalog);

        return catalog;
    }

    // What an owner should fix for better agent behaviour, once per registry revision.
    private void Report(ToolCatalog catalog)
    {
        if (catalog.MissingDescriptions.Count > 0)
        {
            LogMissingDescriptions(logger, catalog.MissingDescriptions.Count, string.Join(", ", catalog.MissingDescriptions));
        }

        if (catalog.TruncatedNames.Count > 0)
        {
            LogTruncatedNames(logger, catalog.TruncatedNames.Count, string.Join(", ", catalog.TruncatedNames));
        }

        if (catalog.NameCollisions.Count > 0)
        {
            LogNameCollisions(logger, catalog.NameCollisions.Count, string.Join(", ", catalog.NameCollisions));
        }

        if (catalog.UnresolvedLookups.Count > 0)
        {
            LogUnresolvedLookups(logger, catalog.UnresolvedLookups.Count, string.Join(", ", catalog.UnresolvedLookups));
        }

        if (catalog.IsPartial)
        {
            LogPartialRegistry(logger, string.Join(", ", catalog.UnavailableServices));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: {Count} tool(s) or argument(s) have no description, so agents must guess what they mean. Add [Description] to: {Missing}.")]
    private static partial void LogMissingDescriptions(ILogger logger, int count, string missing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: {Count} tool name(s) exceeded MCP's 64-character limit and were shortened; rename the capability types (KAL2019): {Names}.")]
    private static partial void LogTruncatedNames(ILogger logger, int count, string names);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: {Count} capability name(s) collide, so their tools carry a kind suffix; rename the capability types so each name says what it is: {Collisions}.")]
    private static partial void LogNameCollisions(ILogger logger, int count, string collisions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: {Count} step input lookup(s) name a query that is not published or not visible to this caller, so agents must ask for the value: {Lookups}.")]
    private static partial void LogUnresolvedLookups(ILogger logger, int count, string lookups);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: the registry is partial; tools from {Services} are missing until those services respond.")]
    private static partial void LogPartialRegistry(ILogger logger, string services);
}
