using Kaleido.Http.Registry;
using Kaleido.Processor.Registry;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http;

public static class KaleidoEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all registered Kaleido endpoints in one call.
    /// The framework auto-detects which runtimes are active (Process and/or Queryable)
    /// and maps only those, plus the unified <c>GET /{service}/registry</c> endpoint.
    /// Set <see cref="KaleidoHttpMapOptions.AggregateRegistry"/> to also fan out to
    /// downstream services registered via <c>AddHttpClients()</c> (routers/gateways).
    /// Returns an <see cref="IEndpointConventionBuilder"/> that
    /// propagates conventions (e.g. <c>.RequireAuthorization()</c>) to all mapped endpoints.
    /// </summary>
    public static IEndpointConventionBuilder MapKaleidoHttp(
        this IEndpointRouteBuilder endpoints,
        Action<KaleidoHttpMapOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var mapOptions = new KaleidoHttpMapOptions();
        configure?.Invoke(mapOptions);

        var hasProcess = endpoints.ServiceProvider.GetService<IProcessorStepRegistry>() is not null;
        var hasQueryable = endpoints.ServiceProvider.GetService<IQueryableRegistry>() is not null;

        var stepCount = hasProcess
            ? endpoints.ServiceProvider.GetRequiredService<IProcessorStepRegistry>().Registrations.Count
            : 0;

        var contextCount = 0;
        var viewCount = 0;

        if (hasQueryable)
        {
            var registrations = endpoints.ServiceProvider
                .GetRequiredService<IQueryableRegistry>()
                .Registrations;

            contextCount = registrations.Count;
            viewCount = registrations.Sum(c => c.Views.Count);
        }

        endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kaleido.Startup")
            .LogInformation(
                "Kaleido started: {StepCount} process step(s), {ContextCount} query context(s), {ViewCount} query view(s).",
                stepCount,
                contextCount,
                viewCount);

        var builders = new List<IEndpointConventionBuilder>();

        if (hasProcess)
        {
            builders.Add(
                endpoints.MapProcessor());
        }

        if (hasQueryable)
        {
            builders.Add(
                endpoints.MapQueryable());
        }

        // Every Kaleido host exposes its registry; aggregation mode fans out to
        // downstream clients (validated inside MapRegistry).
        if (hasProcess || hasQueryable || mapOptions.AggregateRegistry)
        {
            builders.Add(
                endpoints.MapRegistry(mapOptions));
        }

        return new RouteHandlerBuilder(builders);
    }
}
