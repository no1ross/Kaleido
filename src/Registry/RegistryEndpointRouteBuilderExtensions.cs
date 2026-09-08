using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Client;
using Kaleido.Process.Registry;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Registry;

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
        this IEndpointRouteBuilder endpoints,
        Action<RegistryRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new RegistryRouteOptions();
        configure?.Invoke(options);

        // Resolved once at map-time — these do not change after startup.
        var processClientMap = endpoints.ServiceProvider
            .GetService<KaleidoProcessClientRouteOptionsMap>();

        var queryableClientMap = endpoints.ServiceProvider
            .GetService<KaleidoQueryableClientRouteOptionsMap>();

        // Optional — only present when the host has called AddQueryable().
        var localQueryableRegistry = endpoints.ServiceProvider
            .GetService<IQueryableRegistry>();

        var localQueryableRouteOptions = endpoints.ServiceProvider
            .GetService<QueryableRouteOptions>();

        endpoints.MapGet(
                RegistryContractUrls.Registry(options),
                async (
                    IProcessorRegistry localRegistry,
                    ProcessRouteOptions processRouteOptions,
                    IKaleidoProcessClientFactory processClientFactory,
                    IKaleidoQueryableClientFactory queryableClientFactory,
                    CancellationToken cancellationToken) =>
                {
                    var localProcesses =
                        GetLocalProcesses(localRegistry, processRouteOptions);

                    var localQueryables =
                        GetLocalQueryables(localQueryableRegistry, localQueryableRouteOptions);

                    var (downstreamProcesses, processErrors) =
                        await GetDownstreamProcessesAsync(processClientMap, processClientFactory, cancellationToken);

                    var (downstreamQueryables, queryableErrors) =
                        await GetDownstreamQueryablesAsync(queryableClientMap, queryableClientFactory, cancellationToken);

                    return Results.Ok(new AggregatedRegistryResponse
                    {
                        Processes = localProcesses
                            .Concat(downstreamProcesses)
                            .OrderBy(r => r.Name)
                            .ToArray(),
                        Queryables = localQueryables
                            .Concat(downstreamQueryables)
                            .OrderBy(r => r.Name)
                            .ToArray(),
                        ClientErrors = [..processErrors, ..queryableErrors]
                    });
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
        IProcessorRegistry registry,
        ProcessRouteOptions options)
        => registry.Registrations
            .Select(r => ProcessorRegistryResponseFactory.FromRegistration(r, options));

    private static IEnumerable<QueryableRecordResponse> GetLocalQueryables(
        IQueryableRegistry? registry,
        QueryableRouteOptions? options)
    {
        if (registry is null || options is null)
            return [];

        return registry.Registrations
            .Select(r => QueryableRecordResponse.FromRegistryItem(r, options));
    }

    private static async Task<(IReadOnlyCollection<ProcessorRegistryResponse> Items, IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamProcessesAsync(
            KaleidoProcessClientRouteOptionsMap? map,
            IKaleidoProcessClientFactory factory,
            CancellationToken cancellationToken)
    {
        if (map is null)
            return ([], []);

        var items = new List<ProcessorRegistryResponse>();
        var errors = new List<RegistryClientError>();

        await Task.WhenAll(
            map.Options.Keys.Select(async name =>
            {
                try
                {
                    var result = await factory.GetClient(name).GetRegistryAsync(cancellationToken);
                    lock (items) items.AddRange(result);
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(new RegistryClientError
                    {
                        ClientName = name,
                        ClientType = "Process",
                        Reason = ex.Message
                    });
                }
            }));

        return (items, errors);
    }

    private static async Task<(IReadOnlyCollection<QueryableRecordResponse> Items, IReadOnlyCollection<RegistryClientError> Errors)>
        GetDownstreamQueryablesAsync(
            KaleidoQueryableClientRouteOptionsMap? map,
            IKaleidoQueryableClientFactory factory,
            CancellationToken cancellationToken)
    {
        if (map is null)
            return ([], []);

        var items = new List<QueryableRecordResponse>();
        var errors = new List<RegistryClientError>();

        await Task.WhenAll(
            map.Options.Keys.Select(async name =>
            {
                try
                {
                    var result = await factory.GetClient(name).GetRegistryAsync(cancellationToken);
                    lock (items) items.AddRange(result);
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(new RegistryClientError
                    {
                        ClientName = name,
                        ClientType = "Queryable",
                        Reason = ex.Message
                    });
                }
            }));

        return (items, errors);
    }
}
