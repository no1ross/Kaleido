using Kaleido.Process.AspNetCore.Srevices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.AspNetCore;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    public static IProcessorBuilder AddProcessorAspNetCore(this IProcessorBuilder builder, 
        Action<ProcessRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IProcessorRuntime)))
        {
            throw new InvalidOperationException("AddProcessor must be called before AddProcessorAspNetCore.");
        }

        var routeOptions = new ProcessRouteOptions(); 
        configure?.Invoke(routeOptions); 
        builder.Services.AddSingleton(routeOptions);

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
        builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();

        return builder;
    }
}
