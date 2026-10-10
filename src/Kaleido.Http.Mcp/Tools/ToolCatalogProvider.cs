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

internal sealed class ToolCatalogProvider(
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
                Unavailable = "The host has not mapped its Kaleido registry; call MapKaleidoHttp(o => o.AggregateRegistry = true) before MapKaleidoMcp()."
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
            logger.LogWarning(
                "Kaleido MCP: {Count} tool(s) or argument(s) have no description, so agents must guess what they mean. Add [Description] to: {Missing}.",
                catalog.MissingDescriptions.Count,
                string.Join(", ", catalog.MissingDescriptions));
        }

        if (catalog.TruncatedNames.Count > 0)
        {
            logger.LogWarning(
                "Kaleido MCP: {Count} tool name(s) exceeded MCP's 64-character limit and were shortened; rename the capability types (KAL2019): {Names}.",
                catalog.TruncatedNames.Count,
                string.Join(", ", catalog.TruncatedNames));
        }

        if (catalog.UnresolvedLookups.Count > 0)
        {
            logger.LogWarning(
                "Kaleido MCP: {Count} step input lookup(s) name a query that is not published or not visible to this caller, so agents must ask for the value: {Lookups}.",
                catalog.UnresolvedLookups.Count,
                string.Join(", ", catalog.UnresolvedLookups));
        }

        if (catalog.IsPartial)
        {
            logger.LogWarning(
                "Kaleido MCP: the registry is partial; tools from {Services} are missing until those services respond.",
                string.Join(", ", catalog.UnavailableServices));
        }
    }
}
