using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kaleido.Http.Mcp.Http;
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
            return cached.Catalog;
        }

        var read = await apiClient.GetRegistryAsync(cached?.Catalog.Revision, cancellationToken);

        ToolCatalog catalog;

        if (read.NotModified && cached is not null)
        {
            catalog = cached.Catalog;
        }
        else
        {
            catalog = read.Registry is { } registry
                ? catalogBuilder.Build(registry)
                : ToolCatalog.Empty;

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
