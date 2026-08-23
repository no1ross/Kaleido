using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.AspNetCore;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    public static IParticipantBuilder AddParticipantAspNetCore(this IParticipantBuilder builder, 
        Action<ProcessRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IParticipantRuntime)))
        {
            throw new InvalidOperationException("AddParticipant must be called before AddParticipantAspNetCore.");
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
