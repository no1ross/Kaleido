using System.Collections.Concurrent;
using Kaleido.Http.Authorization;
using Kaleido.Http.Registry.Contracts;
using Kaleido.Process.Registry;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Registry;

public static class RegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the unified registry endpoint at <c>GET /{service}/registry</c>.
    /// Returns a single <see cref="AggregatedRegistryResponse"/> containing this
    /// service's local process and queryable registrations; when
    /// <paramref name="aggregate"/> is set the response also merges every
    /// downstream client registered via <c>AddHttpClients()</c>.
    /// <c>ClientErrors</c> reports downstream clients that failed — the endpoint
    /// always returns HTTP 200; a non-empty collection means a partial response.
    /// </summary>
    internal static RouteGroupBuilder MapRegistry(
        this IEndpointRouteBuilder endpoints,
        bool aggregate)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Resolved once at map-time — these do not change after startup.
        var processClientMap = endpoints.ServiceProvider
            .GetService<KaleidoProcessClientRouteOptionsMap>();

        var queryableClientMap = endpoints.ServiceProvider
            .GetService<KaleidoQueryableClientRouteOptionsMap>();

        // Guard — aggregation requires AddHttpClients() with at least one
        // configured client. The route-options maps are singletons registered
        // per client; neither present means no client infrastructure exists.
        if (aggregate && processClientMap is null && queryableClientMap is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProInvalidRegistration,
                "Cannot aggregate the Registry endpoint: no Kaleido clients are registered. " +
                "Call AddHttpClients() on the IKaleidoBuilder before mapping an aggregated registry.");
        }

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
        var cache = endpoints.ServiceProvider.GetService<HttpRegistryCache>() ?? new HttpRegistryCache();

        var group =
            endpoints.MapGroup("")
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        group.MapGet(
                RegistryContractUrls.Registry(localServiceOptions.ServiceName),
                async (
                    HttpContext httpContext,
                    [FromServices] IProcessResponseFactory responseFactory,
                    CancellationToken cancellationToken) =>
                {
                    // Optional — resolve inside the handler so a host that only
                    // registers one client type (or none via AddHttpClients with
                    // no clients configured) does not fail endpoint activation.
                    var processClientFactory = httpContext.RequestServices
                        .GetService<IKaleidoProcessClientFactory>();

                    var queryableClientFactory = httpContext.RequestServices
                        .GetService<IKaleidoQueryableClientFactory>();
                    var forceRefresh = httpContext.Request.Query.ContainsKey("refresh");

                    if (!forceRefresh && cache.Current is not null)
                    {
                        logger.LogDebug("Registry cache hit — serving cached response.");
                    }
                    else if (forceRefresh)
                    {
                        logger.LogDebug("Registry cache bypassed (force refresh requested).");
                    }
                    else
                    {
                        logger.LogDebug("Registry cache miss — building fresh response.");
                    }

                    var authorizer =
                        httpContext.RequestServices
                            .GetService<IKaleidoAuthorizer>();

                    var response = await cache.GetOrBuildAsync(forceRefresh, async ct =>
                    {
                        var localProcesses =
                            GetLocalProcesses(localProcessorRegistry, localServiceOptions, responseFactory);

                        var localQueryables =
                            GetLocalQueryables(localQueryableRegistry, localServiceOptions);

                        var (downstreamProcesses, downstreamQueryables, clientErrors) =
                            aggregate
                                ? await GetDownstreamAsync(
                                    DownstreamClientNames(processClientMap, queryableClientMap, localServiceOptions),
                                    processClientMap, processClientFactory,
                                    queryableClientMap, queryableClientFactory,
                                    logger, ct)
                                : ([], [], []);

                        var allProcesses = localProcesses
                            .Concat(downstreamProcesses)
                            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        var entryProcessors = allProcesses
                            .Where(p => p.IsEntryProcessor)
                            .ToArray();

                        if (entryProcessors.Length > 1)
                        {
                            throw new KaleidoConfigurationException(
                                ConfigurationErrorCodes.ProInvalidRegistration,
                                $"Multiple processors are marked as entry processors: {string.Join(", ", entryProcessors.Select(p => p.Name))}. " +
                                "Only one processor in a distributed system should have IsEntryProcessor set to true.");
                        }

                        var result = new AggregatedRegistryResponse
                        {
                            Processes = allProcesses,
                            Queryables = localQueryables
                                .Concat(downstreamQueryables)
                                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                                .ToArray(),
                            ClientErrors = clientErrors
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

                    // Scope the aggregate to the inbound caller's persona —
                    // capabilities the caller may not invoke are omitted.
                    // The cache holds the unfiltered union; filtering is
                    // per-request.
                    if (authorizer is not null)
                    {
                        response =
                            await FilterForCaller(
                                response,
                                authorizer,
                                cancellationToken);
                    }

                    return Results.Ok(response);
                })
            .WithName(RegistryEndpointNames.RegistryEndpointName)
            .WithTags("Registry")
            .Produces<AggregatedRegistryResponse>()
            .WithSummary("Get unified registry.")
            .WithDescription(
                "Returns the combined process and queryable registrations from this service" +
                (aggregate ? " and all registered downstream clients." : ".") +
                " Always returns HTTP 200. Inspect ClientErrors to detect " +
                "partial responses caused by unreachable or misconfigured downstream clients. " +
                "Process steps carry fully-resolved ExecuteUrl and MetadataUrl values. " +
                "Adding a downstream client via AddProcessClient() or AddQueryableClient() makes it appear here automatically.");

        return group;
    }

    private static async Task<AggregatedRegistryResponse> FilterForCaller(
        AggregatedRegistryResponse response,
        IKaleidoAuthorizer authorizer,
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

            processes.Add(
                processor with
                {
                    InitialSteps = initialSteps,
                    Steps = steps
                });
        }

        var allowedQueryables =
            await authorizer.FilterAsync(response.Queryables,
                x => x.Authorization,
                cancellationToken);

        var queryables = new List<QueryableRecordResponse>();

        foreach (var queryable in allowedQueryables)
        {
            var views =
                await authorizer.FilterAsync(queryable.Views,
                    x => x.Authorization,
                    cancellationToken);

            queryables.Add(
                queryable with
                {
                    Views = views
                });
        }

        return response with
        {
            Processes = processes,
            Queryables = queryables
        };
    }

    private static IEnumerable<ProcessorRegistryResponse> GetLocalProcesses(
        IProcessRegistry? registry,
        KaleidoServiceOptions? serviceOptions,
        IProcessResponseFactory responseFactory)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => responseFactory.CreateRegistryResponse(r, serviceOptions))
            : Enumerable.Empty<ProcessorRegistryResponse>();

    private static IEnumerable<QueryableRecordResponse> GetLocalQueryables(
        IQueryableRegistry? registry,
        KaleidoServiceOptions? serviceOptions)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => QueryableRecordResponse.FromRegistryItem(r, serviceOptions.ServiceName))
            : [];

    // Union of all downstream client names. Clients whose RoutePrefix equals
    // this service's name are excluded — an aggregator must never fetch its own
    // registry, or the request would recurse through this endpoint (its local
    // entries are already in the response).
    private static IReadOnlyCollection<string> DownstreamClientNames(
        KaleidoProcessClientRouteOptionsMap? processMap,
        KaleidoQueryableClientRouteOptionsMap? queryableMap,
        KaleidoServiceOptions serviceOptions)
    {
        var prefixes = new List<KeyValuePair<string, string>>();

        if (processMap is not null)
        {
            prefixes.AddRange(processMap.Options);
        }

        if (queryableMap is not null)
        {
            prefixes.AddRange(queryableMap.Options);
        }

        return prefixes
            .Where(x => !string.Equals(
                x.Value,
                serviceOptions.ServiceName,
                StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // One logical fetch per downstream client — process and queryable client
    // registries share the KaleidoRemoteRegistry cache, so the second
    // GetRegistryAsync resolves without another HTTP call.
    private static async Task<(IReadOnlyCollection<ProcessorRegistryResponse> Processes,
                               IReadOnlyCollection<QueryableRecordResponse> Queryables,
                               IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamAsync(
            IReadOnlyCollection<string> clientNames,
            KaleidoProcessClientRouteOptionsMap? processMap,
            IKaleidoProcessClientFactory? processFactory,
            KaleidoQueryableClientRouteOptionsMap? queryableMap,
            IKaleidoQueryableClientFactory? queryableFactory,
            ILogger logger,
            CancellationToken cancellationToken)
    {
        var processes = new ConcurrentBag<ProcessorRegistryResponse>();
        var queryables = new ConcurrentBag<QueryableRecordResponse>();
        var errors = new ConcurrentBag<RegistryClientError>();

        await Task.WhenAll(
            clientNames.Select(async name =>
            {
                try
                {
                    if (processFactory is not null && processMap?.Options.ContainsKey(name) == true)
                    {
                        foreach (var r in await processFactory.GetClient(name).GetRegistryAsync(cancellationToken))
                        {
                            processes.Add(r);
                        }
                    }

                    if (queryableFactory is not null && queryableMap?.Options.ContainsKey(name) == true)
                    {
                        foreach (var r in await queryableFactory.GetClient(name).GetRegistryAsync(cancellationToken))
                        {
                            queryables.Add(r);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Registry client {ClientName} failed: {Reason}.",
                        name,
                        ex.Message);

                    errors.Add(new RegistryClientError
                    {
                        ClientName = name,
                        ClientType = "Registry",
                        Reason = "Registry fetch failed. See server logs for details."
                    });
                }
            }));

        return (processes.ToArray(), queryables.ToArray(), errors.ToArray());
    }
}
