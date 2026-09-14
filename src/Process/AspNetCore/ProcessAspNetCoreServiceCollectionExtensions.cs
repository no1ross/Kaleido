using Kaleido.Process.AspNetCore.Srevices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.AspNetCore;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core process infrastructure, reading <c>Kaleido:RoutePrefix</c>
    /// from the configuration supplied to <see cref="KaleidoServiceCollectionExtensions.AddKaleido"/>.
    /// </summary>
    public static IProcessorBuilder AddProcessorAspNetCore(this IProcessorBuilder builder)
        => builder.AddProcessorAspNetCore(o =>
        {
            var prefix = builder.Configuration[
                $"{KaleidoOptions.SectionName}:{nameof(ProcessRouteOptions.RoutePrefix)}"];
            if (!string.IsNullOrWhiteSpace(prefix))
                o.RoutePrefix = prefix;
        });

    public static IProcessorBuilder AddProcessorAspNetCore(this IProcessorBuilder builder,
        Action<ProcessRouteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IProcessorRuntime)))
        {
            throw new InvalidOperationException("AddProcessor must be called before AddProcessorAspNetCore.");
        }

        var routeOptions = new ProcessRouteOptions();
        configure(routeOptions);
        builder.Services.AddSingleton(routeOptions);

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
        builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();

        return builder;
    }
}
