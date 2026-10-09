using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kaleido.Http.Mcp.Http;
using Kaleido.Http.Mcp.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Gives the current caller their tools. The catalog is translated from the registry read with
/// the caller's own credentials (so it holds only what the caller may use) and reused briefly,
/// keyed by a hash of the caller's <c>Authorization</c> header; after
/// <see cref="KaleidoMcpOptions.RegistryCacheDuration"/> it is revalidated with the registry's
/// revision (<c>If-None-Match</c>), so an unchanged registry costs a 304.
/// </summary>
internal interface IToolCatalogProvider
{
    Task<ToolCatalog> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class ToolCatalogProvider(
    IKaleidoApiClient apiClient,
    IToolCatalogBuilder catalogBuilder,
    IHttpContextAccessor httpContextAccessor,
    KaleidoMcpOptions options,
    TimeProvider timeProvider,
    IMcpObservability observability,
    ILogger<ToolCatalogProvider> logger)
    : IToolCatalogProvider
{
    private const int MaxCachedCallers = 1_000;

    // Shared across scopes: one entry per caller credential.
    private static readonly ConcurrentDictionary<string, CachedCatalog> Cache = new(StringComparer.Ordinal);

    // Revisions already reported, so each gap is logged once rather than on every call.
    private static readonly ConcurrentDictionary<string, byte> ReportedRevisions = new(StringComparer.Ordinal);

    public async Task<ToolCatalog> GetAsync(CancellationToken cancellationToken = default)
    {
        var key = CallerKey();
        var now = timeProvider.GetUtcNow();

        if (Cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
        {
            observability.RegistryRead(McpTelemetry.RegistryHit, cached.Catalog.Revision);

            return cached.Catalog;
        }

        var read = await apiClient.GetRegistryAsync(cached?.Catalog.Revision, cancellationToken);

        if (read.Error is { } error)
        {
            observability.RegistryRead(McpTelemetry.RegistryFailed, cached?.Catalog.Revision);
            logger.LogWarning("Kaleido MCP: {Error}", error);

            // Keep serving the caller's last known tools rather than none; retry on the next call.
            return cached?.Catalog is { Unavailable: null } previous
                ? previous
                : new ToolCatalog { Unavailable = error };
        }

        ToolCatalog catalog;

        if (read.NotModified && cached is not null)
        {
            catalog = cached.Catalog;
            observability.RegistryRead(McpTelemetry.RegistryNotModified, catalog.Revision);
        }
        else
        {
            catalog = read.Registry is { } registry
                ? catalogBuilder.Build(registry)
                : ToolCatalog.Empty;

            observability.RegistryRead(McpTelemetry.RegistryFetched, catalog.Revision);
            Report(catalog);
        }

        if (Cache.Count >= MaxCachedCallers)
        {
            Cache.Clear();
        }

        Cache[key] = new CachedCatalog(catalog, now + options.RegistryCacheDuration);

        return catalog;
    }

    private string CallerKey()
    {
        var authorization =
            httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        return string.IsNullOrWhiteSpace(authorization)
            ? "anonymous"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
    }

    // What an owner should fix for better agent behaviour, once per registry revision.
    private void Report(ToolCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(catalog.Revision) ||
            !ReportedRevisions.TryAdd(catalog.Revision, 0))
        {
            return;
        }

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

        if (catalog.IsPartial)
        {
            logger.LogWarning(
                "Kaleido MCP: the registry is partial; tools from {Services} are missing until those services respond.",
                string.Join(", ", catalog.UnavailableServices));
        }
    }

    private sealed record CachedCatalog(ToolCatalog Catalog, DateTimeOffset ExpiresAt);
}
