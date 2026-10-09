using Kaleido.Http.Registry.Contracts;
using Kaleido.Processor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    /// <remarks>
    /// The registry itself (aggregation, the shared snapshot, per-caller filtering and the revision)
    /// is <see cref="IKaleidoRegistryService"/>; this endpoint adds only the HTTP contract:
    /// <c>ETag</c>/<c>Cache-Control</c>, <c>If-None-Match</c> → 304 and <c>?strict</c> → 502.
    /// </remarks>
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

        // Required — AddKaleido() must be called before MapRegistry().
        var localServiceOptions = endpoints.ServiceProvider
            .GetRequiredService<KaleidoServiceOptions>();

        // The registry service serves the shared snapshot with the settings decided here
        // (AddHttp() registers it; without AddHttp() the endpoint fails closed per request).
        endpoints.ServiceProvider
            .GetService<RegistrySnapshot>()?
            .Configure(mapOptions);

        var group =
            endpoints.MapGroup("")
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        var endpoint = group.MapGet(
                RegistryContractUrls.Registry(localServiceOptions.ServiceName),
                async (
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    // Fail closed: the registry is filtered per caller, which needs AddHttp()'s
                    // authorizer — never serve it unfiltered.
                    var registry =
                        httpContext.RequestServices.GetService<IKaleidoRegistryService>()
                        ?? throw new KaleidoConfigurationException(
                            ConfigurationErrorCodes.AuthenticationNotConfigured,
                            "Registry filtering requires AddHttp() to register the Kaleido HTTP authorizer.");

                    var forceRefresh = httpContext.Request.Query.ContainsKey("refresh");
                    var strict = httpContext.Request.Query.ContainsKey("strict");

                    var response = await registry.GetForCallerAsync(forceRefresh, cancellationToken);

                    // Freshness contract: Revision hashes the per-caller filtered payload (same
                    // content → same ETag); Cache-Control advertises the configured staleness window.
                    var etag = $"\"{response.Revision}\"";
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
}
