using System.Globalization;
using Kaleido.Http.Registry;
using Kaleido.Http.Registry.Contracts;
using Kaleido.Platform;
using Kaleido.Platform.Registry;
using Kaleido.Processor;
using Kaleido.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Server.Registry;

internal static class RegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the unified registry endpoint at <c>GET /{service}/registry</c>.
    /// Returns a single <see cref="KaleidoRegistry"/> containing this
    /// service's local process and queryable registrations; when
    /// <paramref name="mapOptions"/>.<c>AggregateRegistry</c> is set the response also merges every
    /// downstream client registered via <c>AddHttpClients()</c>.
    /// <c>Errors</c> reports failed downstream clients. Partial aggregates
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

        var aggregate = mapOptions.AggregateRegistry;

        EnsureClientsForAggregation(endpoints.ServiceProvider, aggregate);

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
            .AddEndpointFilter<KaleidoExceptionEndpointFilter>()
            .AddEndpointFilter<Kaleido.Server.Observability.KaleidoCorrelationEndpointFilter>()
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        var endpoint = group.MapGet(
                RegistryContractUrls.Registry(localServiceOptions.ServiceName),
                (HttpContext httpContext, CancellationToken cancellationToken) =>
                    GetRegistryAsync(httpContext, mapOptions, cancellationToken))
            .WithName(RegistryEndpointNames.RegistryEndpointName)
            .WithTags("Registry")
            .Produces<KaleidoRegistry>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesKaleidoErrors()
            .WithSummary("Get unified registry.")
            .WithDescription(Description(aggregate));

        if (aggregate)
        {
            endpoint.Produces<KaleidoRegistry>(StatusCodes.Status502BadGateway);
        }

        return group;
    }

    // Guard — aggregation requires AddHttpClients() with at least one configured client. The
    // client catalog is registered with the first client; without it no client infrastructure exists.
    // Resolved once at map-time — it does not change after startup.
    private static void EnsureClientsForAggregation(IServiceProvider services, bool aggregate)
    {
        if (aggregate &&
            services.GetService<IKaleidoClientCatalog>() is null)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                "Cannot aggregate the Registry endpoint: no Kaleido clients are registered. " +
                "Call AddHttpClients() on the IKaleidoBuilder before mapping an aggregated registry.");
        }
    }

    private static async Task<IResult> GetRegistryAsync(
        HttpContext httpContext,
        KaleidoHttpMapOptions mapOptions,
        CancellationToken cancellationToken)
    {
        // Fail closed: the registry is filtered per caller, which needs AddHttp()'s
        // authorizer — never serve it unfiltered.
        var registry =
            httpContext.RequestServices.GetService<IKaleidoRegistryService>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.AuthenticationNotConfigured,
                "Registry filtering requires AddHttp() to register the Kaleido HTTP authorizer.");

        var forceRefresh = httpContext.Request.Query.ContainsKey("refresh");

        var response = await registry.GetForCallerAsync(forceRefresh, cancellationToken);

        return httpContext.RegistryResult(response, mapOptions.RegistryCacheTtl);
    }

    /// <summary>
    /// The registry's HTTP answer, shared with the control registry (#553): the ETag (the revision) and
    /// <c>Cache-Control</c>, 304 when <c>If-None-Match</c> matches, and 502 with the body for a partial
    /// response under <c>?strict</c>.
    /// </summary>
    internal static IResult RegistryResult(this HttpContext httpContext, KaleidoRegistry response, TimeSpan? cacheTtl)
    {
        var strict = httpContext.Request.Query.ContainsKey("strict");

        // Freshness contract: Revision hashes the per-caller filtered payload (same
        // content → same ETag); Cache-Control advertises the configured staleness window. Private:
        // the registry is filtered per caller, so a shared cache must never serve it to another one.
        var etag = $"\"{response.Revision}\"";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl =
            cacheTtl is { } ttl
                ? string.Create(CultureInfo.InvariantCulture, $"private, max-age={(int)ttl.TotalSeconds}")
                : "private, no-cache";

        if (httpContext.Request.Headers.IfNoneMatch.Any(
                value => string.Equals(value, etag, StringComparison.Ordinal)
                    || string.Equals(value, "*", StringComparison.Ordinal)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        // ?strict — agents/gateways get a real failure signal when
        // the aggregate is partial; the full body is still included.
        if (strict && response.IsPartial)
        {
            return Results.Json(
                response,
                KaleidoJsonOptions.Options,
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(response);
    }

    private static string Description(bool aggregate) =>
        "Returns process and queryable registrations from this service" +
        (aggregate ? " and configured downstream clients. " : ". ") +
        (aggregate
            ? "A partial aggregate returns HTTP 200 by default with isPartial and errors; " +
              "?strict returns HTTP 502 with the same body. Partial results are not cached. "
            : "A successful local-only registry request returns HTTP 200. ") +
        "Process steps carry fully-resolved executeUrl values. " +
        (aggregate
            ? "Configured downstream clients are registered via AddHttpClients()."
            : "To include downstream clients, enable AggregateRegistry and call AddHttpClients().");
}
