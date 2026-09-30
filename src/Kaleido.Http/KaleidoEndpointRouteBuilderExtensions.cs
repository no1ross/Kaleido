using Kaleido.Process.Registry;
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
    /// and maps only those. Returns an <see cref="IEndpointConventionBuilder"/> that
    /// propagates conventions (e.g. <c>.RequireAuthorization()</c>) to all mapped endpoints.
    /// </summary>
    public static IEndpointConventionBuilder MapKaleido(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var hasProcess = endpoints.ServiceProvider.GetService<IProcessStepRegistry>() is not null;
        var hasQueryable = endpoints.ServiceProvider.GetService<IQueryableRegistry>() is not null;

        var stepCount = hasProcess
            ? endpoints.ServiceProvider.GetRequiredService<IProcessStepRegistry>().Registrations.Count
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

        return new RouteHandlerBuilder(builders);
    }
}
