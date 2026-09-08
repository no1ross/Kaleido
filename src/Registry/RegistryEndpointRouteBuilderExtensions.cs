using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Client;
using Kaleido.Process.Registry;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
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
    ///     <c>Queryables</c> — all queryable clients registered via <c>AddQueryableClient()</c>.
    ///   </description></item>
    /// </list>
    /// Each collection is only populated when the corresponding client infrastructure has
    /// been registered. Adding a new downstream client makes it appear automatically.
    /// </summary>
    public static IEndpointRouteBuilder MapRegistry(
        this IEndpointRouteBuilder endpoints,
        Action<RegistryRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new RegistryRouteOptions();
        configure?.Invoke(options);

        var processClientMap = endpoints.ServiceProvider
            .GetService<KaleidoProcessClientRouteOptionsMap>();

        var queryableClientMap = endpoints.ServiceProvider
            .GetService<KaleidoQueryableClientRouteOptionsMap>();

        endpoints.MapGet(
                RegistryContractUrls.Registry(options),
                async (
                    IProcessorRegistry localRegistry,
                    ProcessRouteOptions processRouteOptions,
                    IKaleidoProcessClientFactory processClientFactory,
                    IKaleidoQueryableClientFactory queryableClientFactory,
                    CancellationToken cancellationToken) =>
                {
                    // Processes — local + all downstream process clients
                    var localProcesses = localRegistry.Registrations
                        .Select(r => ProcessorRegistryResponseFactory.FromRegistration(r, processRouteOptions));

                    var downstreamProcesses = processClientMap is not null
                        ? await Task.WhenAll(
                            processClientMap.Options.Keys.Select(async name =>
                            {
                                try
                                {
                                    return await processClientFactory
                                        .GetClient(name)
                                        .GetRegistryAsync(cancellationToken);
                                }
                                catch
                                {
                                    return (IReadOnlyList<ProcessorRegistryResponse>)[];
                                }
                            }))
                        : [];

                    // Queryables — all downstream queryable clients
                    var downstreamQueryables = queryableClientMap is not null
                        ? await Task.WhenAll(
                            queryableClientMap.Options.Keys.Select(async name =>
                            {
                                try
                                {
                                    return await queryableClientFactory
                                        .GetClient(name)
                                        .GetRegistryAsync(cancellationToken);
                                }
                                catch
                                {
                                    return (IReadOnlyList<QueryableRecordResponse>)[];
                                }
                            }))
                        : [];

                    return Results.Ok(new AggregatedRegistryResponse
                    {
                        Processes = localProcesses
                            .Concat(downstreamProcesses.SelectMany(r => r))
                            .OrderBy(r => r.Name)
                            .ToArray(),
                        Queryables = downstreamQueryables
                            .SelectMany(r => r)
                            .OrderBy(r => r.Name)
                            .ToArray()
                    });
                })
            .WithName("GetAggregatedRegistry")
            .WithTags("Registry")
            .Produces<AggregatedRegistryResponse>()
            .WithSummary("Get unified registry.")
            .WithDescription(
                "Returns the combined process and queryable registrations from this processor and all " +
                "registered downstream clients. Process steps carry fully-resolved ExecuteUrl and " +
                "MetadataUrl values. Adding a downstream client via AddProcessClient() or " +
                "AddQueryableClient() makes it appear here automatically.");

        return endpoints;
    }
}
