using System.Collections.Concurrent;
using Kaleido.Http.Authorization;
using Kaleido.Http.Registry.Contracts;
using Kaleido.Processor;
using Kaleido.Processor.Registry;
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
    /// <paramref name="mapOptions"/>.<c>AggregateRegistry</c> is set the response also merges every
    /// downstream client registered via <c>AddHttpClients()</c>.
    /// <c>ClientErrors</c> reports failed downstream clients. Partial aggregates
    /// return HTTP 200 by default, or HTTP 502 with the same body when
    /// <c>?strict</c> is requested.
    /// </summary>
    internal static RouteGroupBuilder MapRegistry(
        this IEndpointRouteBuilder endpoints,
        KaleidoHttpMapOptions mapOptions)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Resolved once at map-time — these do not change after startup.
        var processClientMap = endpoints.ServiceProvider
            .GetService<KaleidoProcessorClientRouteOptionsMap>();

        var queryableClientMap = endpoints.ServiceProvider
            .GetService<KaleidoQueryableClientRouteOptionsMap>();

        var aggregate = mapOptions.AggregateRegistry;

        // Guard — aggregation requires AddHttpClients() with at least one
        // configured client. The route-options maps are singletons registered
        // per client; neither present means no client infrastructure exists.
        if (aggregate && processClientMap is null && queryableClientMap is null)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                "Cannot aggregate the Registry endpoint: no Kaleido clients are registered. " +
                "Call AddHttpClients() on the IKaleidoBuilder before mapping an aggregated registry.");
        }

        // Optional — only present when the host has called AddHttp().
        var localProcessorRegistry = endpoints.ServiceProvider
            .GetService<IProcessorRegistry>();

        // Required — AddKaleido() must be called before MapRegistry().
        var localServiceOptions = endpoints.ServiceProvider
            .GetRequiredService<KaleidoServiceOptions>();

        var logger = endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kaleido.Registry");

        // Optional — only present when the host has called AddQueryable().
        var localQueryableRegistry = endpoints.ServiceProvider
            .GetService<IQueryableRegistry>();

        // Snapshot store — IRegistrySnapshotStore from DI (distributed impls
        // registered by the host) or the in-memory default. The cache is
        // singleton-scoped to this endpoint either way.
        var snapshotStore =
            endpoints.ServiceProvider.GetService<IRegistrySnapshotStore>()
            ?? new InMemoryRegistrySnapshotStore();

        // Canonical key — kaleido:{ServiceName}. Consumers reading this key
        // from a shared store get exactly what this endpoint returns (the
        // merged aggregate on a router, local registrations on a leaf).
        var cache = new HttpRegistryCache(
            snapshotStore,
            $"kaleido:{localServiceOptions.ServiceName}");

        var group =
            endpoints.MapGroup("")
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        var endpoint = group.MapGet(
                RegistryContractUrls.Registry(localServiceOptions.ServiceName),
                async (
                    HttpContext httpContext,
                    [FromServices] IProcessorResponseFactory responseFactory,
                    CancellationToken cancellationToken) =>
                {
                    // Optional — resolve inside the handler so a host that only
                    // registers one client type (or none via AddHttpClients with
                    // no clients configured) does not fail endpoint activation.
                    var processClientFactory = httpContext.RequestServices
                        .GetService<IKaleidoProcessorClientFactory>();

                    var queryableClientFactory = httpContext.RequestServices
                        .GetService<IKaleidoQueryableClientFactory>();
                    var forceRefresh = httpContext.Request.Query.ContainsKey("refresh");
                    var strict = httpContext.Request.Query.ContainsKey("strict");

                    var authorizer =
                        httpContext.RequestServices
                            .GetService<IKaleidoAuthorizer>();

                    if (localServiceOptions.AuthorizationMode != KaleidoAuthorizationMode.None
                        && authorizer is null)
                    {
                        throw new KaleidoConfigurationException(
                            ConfigurationErrorCodes.AuthenticationNotConfigured,
                            "Registry filtering requires AddHttp() to register the Kaleido HTTP authorizer.");
                    }

                    var downstreamNames = aggregate
                        ? DownstreamClientNames(processClientMap, queryableClientMap, localServiceOptions)
                        : [];

                    var response = await cache.GetOrBuildAsync(
                        mapOptions.RegistryCacheTtl,
                        mapOptions.RegistryRefreshCooldown,
                        forceRefresh,
                        async ct =>
                    {
                        // Honored refreshes invalidate downstream client caches
                        // first — the rebuild re-fetches, not replays stale
                        // per-client snapshots.
                        if (forceRefresh && aggregate)
                        {
                            InvalidateDownstream(
                                downstreamNames,
                                processClientFactory,
                                queryableClientFactory,
                                logger);
                        }

                        var localProcesses =
                            GetLocalProcesses(localProcessorRegistry, localServiceOptions, responseFactory);

                        var localQueryables =
                            GetLocalQueryables(localQueryableRegistry, localServiceOptions);

                        var (downstreamProcesses, downstreamQueryables, clientErrors) =
                            aggregate
                                ? await GetDownstreamAsync(
                                    downstreamNames,
                                    processClientMap, processClientFactory,
                                    queryableClientMap, queryableClientFactory,
                                    logger,
                                    mapOptions.RegistryDownstreamMaxParallelism,
                                    ct)
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
                                ProcessorErrorCodes.InvalidRegistration,
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

                        if (forceRefresh)
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
                    }, cancellationToken);

                    // Scope the aggregate to the inbound caller's persona —
                    // capabilities the caller may not invoke are omitted.
                    // The cache holds the unfiltered union; filtering is
                    // per-request.
                    if (authorizer?.IsEnforced == true)
                    {
                        response =
                            await FilterForCaller(
                                response,
                                authorizer,
                                cancellationToken);
                    }

                    // Freshness contract: Revision hashes the per-caller
                    // filtered payload (same content → same ETag); Cache-Control
                    // advertises the configured staleness window.
                    var revision = ComputeRevision(response);
                    response = response with { Revision = revision };

                    var etag = $"\"{revision}\"";
                    httpContext.Response.Headers.ETag = etag;
                    httpContext.Response.Headers.CacheControl =
                        mapOptions.RegistryCacheTtl is { } ttl
                            ? $"public, max-age={(int)ttl.TotalSeconds}"
                            : "no-cache";

                    if (httpContext.Request.Headers.IfNoneMatch.Any(
                            value => string.Equals(value, etag, StringComparison.Ordinal)
                                || string.Equals(value, "*", StringComparison.Ordinal)))
                    {
                        return Results.StatusCode(StatusCodes.Status304NotModified);
                    }

                    // ?strict — agents/gateways get a real failure signal when
                    // the aggregate is partial; the full body is still included.
                    if (strict && response.ClientErrors.Count > 0)
                    {
                        return Results.Json(
                            response,
                            KaleidoJsonOptions.Options,
                            statusCode: StatusCodes.Status502BadGateway);
                    }

                    return Results.Ok(response);
                })
            .WithName(RegistryEndpointNames.RegistryEndpointName)
            .WithTags("Registry")
            .Produces<AggregatedRegistryResponse>()
            .WithSummary("Get unified registry.")
            .WithDescription(
                "Returns process and queryable registrations from this service" +
                (aggregate ? " and configured downstream clients. " : ". ") +
                (aggregate
                    ? "A partial aggregate returns HTTP 200 by default with isPartial and clientErrors; " +
                      "?strict returns HTTP 502 with the same body. Partial results are not cached. "
                    : "A successful local-only registry request returns HTTP 200. ") +
                "Process steps carry fully-resolved executeUrl values. " +
                (aggregate
                    ? "Configured downstream clients are registered via AddHttpClients()."
                    : "To include downstream clients, enable AggregateRegistry and call AddHttpClients()."));

        if (aggregate)
        {
            endpoint.Produces<AggregatedRegistryResponse>(StatusCodes.Status502BadGateway);
        }

        return group;
    }

    // SHA-256 over the filtered payload — same content always produces the
    // same revision, so it is valid as an ETag (and safe to round-trip via
    // If-None-Match) per caller persona.
    private static string ComputeRevision(AggregatedRegistryResponse response)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            response with { Revision = null },
            KaleidoJsonOptions.Options);

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }

    // On a real rebuild, drop each downstream client's cached registry first so
    // GetRegistryAsync below performs a genuine re-fetch instead of replaying
    // the shared per-client snapshot.
    private static void InvalidateDownstream(
        IReadOnlyCollection<string> clientNames,
        IKaleidoProcessorClientFactory? processFactory,
        IKaleidoQueryableClientFactory? queryableFactory,
        ILogger logger)
    {
        foreach (var name in clientNames)
        {
            try
            {
                if (processFactory is not null)
                {
                    processFactory.GetClient(name).InvalidateRegistry();
                }
                else
                {
                    queryableFactory?.GetClient(name).InvalidateRegistry();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to invalidate registry cache for client {ClientName}.", name);
            }
        }
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

            // A processor whose every step was filtered out is invisible to
            // the caller — don't advertise an empty shell.
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
                    Steps = steps
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
        IProcessorRegistry? registry,
        KaleidoServiceOptions? serviceOptions,
        IProcessorResponseFactory responseFactory)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => responseFactory.CreateRegistryResponse(r, serviceOptions))
            : Enumerable.Empty<ProcessorRegistryResponse>();

    private static IEnumerable<QueryableSourceResponse> GetLocalQueryables(
        IQueryableRegistry? registry,
        KaleidoServiceOptions? serviceOptions)
        => registry is not null && serviceOptions is not null
            ? registry.Registrations.Select(r => QueryableSourceResponse.FromRegistryItem(r, serviceOptions.ServiceName))
            : [];

    // Union of all downstream client names. Clients whose RoutePrefix equals
    // this service's name are excluded — an aggregator must never fetch its own
    // registry, or the request would recurse through this endpoint (its local
    // entries are already in the response).
    private static IReadOnlyCollection<string> DownstreamClientNames(
        KaleidoProcessorClientRouteOptionsMap? processMap,
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
                               IReadOnlyCollection<QueryableSourceResponse> Queryables,
                               IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamAsync(
            IReadOnlyCollection<string> clientNames,
            KaleidoProcessorClientRouteOptionsMap? processMap,
            IKaleidoProcessorClientFactory? processFactory,
            KaleidoQueryableClientRouteOptionsMap? queryableMap,
            IKaleidoQueryableClientFactory? queryableFactory,
            ILogger logger,
            int? maxParallelism,
            CancellationToken cancellationToken)
    {
        var processes = new ConcurrentBag<ProcessorRegistryResponse>();
        var queryables = new ConcurrentBag<QueryableSourceResponse>();
        var errors = new ConcurrentBag<RegistryClientError>();

        // Bounds the fan-out when a router aggregates many downstreams —
        // null = unbounded (client count is the natural bound).
        using var gate = maxParallelism is { } max
            ? new SemaphoreSlim(max)
            : null;

        await Task.WhenAll(
            clientNames.Select(async name =>
            {
                if (gate is not null)
                {
                    await gate.WaitAsync(cancellationToken);
                }

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
                finally
                {
                    gate?.Release();
                }
            }));

        return (processes.ToArray(), queryables.ToArray(), errors.ToArray());
    }
}
