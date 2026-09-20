using Kaleido.AspNetCore.Process.Services;
using Kaleido.Exceptions;
using Kaleido.Process.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.AspNetCore.Process;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core process infrastructure.
    /// Routes are derived from <see cref="KaleidoServiceOptions.ServiceName"/>
    /// (e.g. <c>"intake"</c> → <c>/intake/processes/…</c>).
    /// </summary>
    internal static IKaleidoBuilder AddProcessorAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IProcessorRegistry)))
        {
            // Process runtime not registered (Queryable-only service) - this is valid
            return builder;
        }

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
        builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();

        return builder;
    }
}
