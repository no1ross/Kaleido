using Kaleido.Process.AspNetCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.AspNetCore;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core process infrastructure.
    /// Routes are derived from <see cref="KaleidoServiceOptions.ServiceName"/>
    /// (e.g. <c>"intake"</c> → <c>/intake/processes/…</c>).
    /// </summary>
    public static IProcessorBuilder AddProcessorAspNetCore(this IProcessorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IProcessorRuntime)))
            throw new InvalidOperationException("AddProcessor must be called before AddProcessorAspNetCore.");

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
        builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();

        return builder;
    }
}
