using Kaleido.Http.Queryable.Contracts;
using Kaleido.Process.Registry;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Kaleido.Http.Registry;

public static class RegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the unified registry endpoint at <c>GET /{routePrefix}/registry</c>.
    /// Returns a single <see cref="AggregatedRegistryResponse"/> containing:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>Processes</c> — this processor's local steps merged with all downstream
    ///     processors registered via <c>AddProcessClient()</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>Queryables</c> — this processor's local queryable contexts (when
    ///     <c>AddQueryable()</c> has been called) merged with all downstream queryable
    ///     clients registered via <c>AddQueryableClient()</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>ClientErrors</c> — any downstream clients that were unreachable or returned
    ///     errors. The endpoint always returns HTTP 200 — a non-empty
    ///     <c>ClientErrors</c> collection means the response is partial.
    ///   </description></item>
    /// </list>
    /// Adding a new downstream client makes it appear automatically.
    /// </summary>
    public static IEndpointRouteBuilder MapRegistry(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Resolved once at map-time — these do not change after startup.
        var processClientMap = endpoints.ServiceProvider
            .GetService<KaleidoProcessClientRouteOptionsMap>();

        var queryableClientMap = endpoints.ServiceProvider
            .GetService<KaleidoQueryableClientRouteOptionsMap>();

        // Optional — only present when the host has called AddHttp().
        var localProcessorRegistry = endpoints.ServiceProvider
            .GetService<IProcessRegistry>();

        // Required — AddKaleido() must be called before MapRegistry().
        var localServiceOptions = endpoints.ServiceProvider
            .GetRequiredService<KaleidoServiceOptions>();

        var logger = endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kaleido.Registry");

        // Optional — only present when the host has called AddQueryable().
        var localQueryableRegistry = endpoints.ServiceProvider
            .GetService<IQueryableRegistry>();

        // Resolve from DI if pre-registered, otherwise allocate a local instance
        // captured in the closure — either way it is singleton-scoped to this endpoint.
        var cache = endpoints.ServiceProvider.GetService<RegistryCache>() ?? new RegistryCache();

        endpoints.MapGet(
                RegistryContractUrls.Registry(localServiceOptions.ServiceName),
                async (
                    HttpContext httpContext,
                    IKaleidoProcessClientFactory processClientFactory,
                    IKaleidoQueryableClientFactory queryableClientFactory,
                    CancellationToken cancellationToken) =>
                {
                    var forceRefresh = httpContext.Request.Query.ContainsKey("refresh");

                    if (!forceRefresh && cache.Current is not null)
                    {
                        logger.LogDebug("Registry cache hit — serving cached response.");
                    }
                    else
                    {
                        logger.LogDebug(
                            forceRefresh
                                ? "Registry cache bypassed (force refresh requested)."
                                : "Registry cache miss — building fresh response.");
                    }

                    var response = await cache.GetOrBuildAsync(forceRefresh, async ct =>
                    {
                        var localProcesses =
                            GetLocalProcesses(localProcessorRegistry, localServiceOptions);

                        var localQueryables =
                            GetLocalQueryables(localQueryableRegistry, localServiceOptions);

                        var (downstreamProcesses, processErrors) =
                            await GetDownstreamProcessesAsync(processClientMap, processClientFactory, logger, ct);

                        var (downstreamQueryables, queryableErrors) =
                            await GetDownstreamQueryablesAsync(queryableClientMap, queryableClientFactory, logger, ct);

                        var allProcesses = localProcesses
                            .Concat(downstreamProcesses)
                            .OrderBy(r => r.Name)
                            .ToArray();

                        var entryProcessors = allProcesses
                            .Where(p => p.IsEntryProcessor)
                            .ToArray();

                        if (entryProcessors.Length > 1)
                        {
                            throw new KaleidoFrameworkException(
                                $"Multiple processors are marked as entry processors: {string.Join(", ", entryProcessors.Select(p => p.Name))}. " +
                                "Only one processor in a distributed system should have IsEntryProcessor set to true.");
                        }

                        var result = new AggregatedRegistryResponse
                        {
                            Processes = allProcesses,
                            Queryables = localQueryables
                                .Concat(downstreamQueryables)
                                .OrderBy(r => r.Name)
                                .ToArray(),
                            ClientErrors = [.. processErrors, .. queryableErrors]
                        };

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
                    }, cancellationToken);

                    return Results.Ok(response);
                })
            .WithName("GetAggregatedRegistry")
            .WithTags("Registry")
            .Produces<AggregatedRegistryResponse>()
            .WithSummary("Get unified registry.")
            .WithDescription(
                "Returns the combined process and queryable registrations from this processor and all " +
                "registered downstream clients. Always returns HTTP 200. Inspect ClientErrors to detect " +
                "partial responses caused by unreachable or misconfigured downstream clients. " +
                "Process steps carry fully-resolved ExecuteUrl and MetadataUrl values. " +
                "Adding a downstream client via AddProcessClient() or AddQueryableClient() makes it appear here automatically.");

        return endpoints;
    }

    private static IEnumerable<ProcessorRegistryResponse> GetLocalProcesses(
        IProcessRegistry? registry,
        KaleidoServiceOptions? serviceOptions)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => ProcessorRegistryResponseFactory.FromRegistration(r, serviceOptions))
            : Enumerable.Empty<ProcessorRegistryResponse>();

    private static IEnumerable<QueryableRecordResponse> GetLocalQueryables(
        IQueryableRegistry? registry,
        KaleidoServiceOptions? serviceOptions)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => QueryableRecordResponse.FromRegistryItem(r, serviceOptions.ServiceName))
            : [];

    private static async Task<(IReadOnlyCollection<ProcessorRegistryResponse> Items, IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamProcessesAsync(
            KaleidoProcessClientRouteOptionsMap? map,
            IKaleidoProcessClientFactory factory,
            ILogger logger,
            CancellationToken cancellationToken)
    {
        if (map is null)
            return ([], []);

        var items = new ConcurrentBag<ProcessorRegistryResponse>();
        var errors = new ConcurrentBag<RegistryClientError>();

        await Task.WhenAll(
            map.Options.Keys.Select(async name =>
            {
                try
                {
                    var result = await factory.GetClient(name).GetRegistryAsync(cancellationToken);
                    foreach (var r in result) items.Add(r);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 is expected for services that don't expose process - swallow it
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Registry process client {ClientName} failed: {Reason}.",
                        name,
                        ex.Message);

                    errors.Add(new RegistryClientError
                    {
                        ClientName = name,
                        ClientType = "Process",
                        Reason = ex.Message
                    });
                }
            }));

        return (items.ToArray(), errors.ToArray());
    }

    private static async Task<(IReadOnlyCollection<QueryableRecordResponse> Items, IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamQueryablesAsync(
            KaleidoQueryableClientRouteOptionsMap? map,
            IKaleidoQueryableClientFactory factory,
            ILogger logger,
            CancellationToken cancellationToken)
    {
        if (map is null)
            return ([], []);

        var items = new ConcurrentBag<QueryableRecordResponse>();
        var errors = new ConcurrentBag<RegistryClientError>();

        await Task.WhenAll(
            map.Options.Keys.Select(async name =>
            {
                try
                {
                    var result = await factory.GetClient(name).GetRegistryAsync(cancellationToken);
                    foreach (var r in result) items.Add(r);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 is expected for services that don't expose queryable - swallow it
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Registry queryable client {ClientName} failed: {Reason}.",
                        name,
                        ex.Message);

                    errors.Add(new RegistryClientError
                    {
                        ClientName = name,
                        ClientType = "Queryable",
                        Reason = ex.Message
                    });
                }
            }));

        return (items.ToArray(), errors.ToArray());
    }
}
