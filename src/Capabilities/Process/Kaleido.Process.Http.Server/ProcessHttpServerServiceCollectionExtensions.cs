using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.Http.Server;

/// <summary>Registers Process-specific ASP.NET execution and state adaptation.</summary>
public static class ProcessHttpServerServiceCollectionExtensions
{
    /// <summary>Registers the Process HTTP adapter without choosing its context, outbox or idempotency store.</summary>
    /// <param name="builder">A host with generic Process, Server and shared HTTP registration.</param>
    /// <param name="options">Process HTTP settings; configuration overrides supplied values.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddProcessHttpServer(this IKaleidoBuilder builder, KaleidoProcessHttpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(x => x.ServiceType == typeof(KaleidoProcessHttpOptions)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.DuplicateRegistration,
                "AddProcessHttpServer() was called more than once; configure Process HTTP in a single call.");
        }

        var configured = builder.BindOptions(options, ConfigurationErrorCodes.InvalidHttpOptions);
        if (!builder.Services.Any(x => x.ServiceType == typeof(IProcessorRuntime))
            || !builder.Services.Any(x => x.ServiceType == typeof(KaleidoServerOptions))
            || !builder.Services.Any(x => x.ServiceType == typeof(KaleidoHttpServerOptions)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions,
                "Call AddProcess(), AddServer() and AddHttp() before AddProcessHttpServer().");
        }

        builder.Services.AddSingleton(configured);
        builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
        builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();
        builder.Services.TryAddScoped<IProcessContinuationFilter, ProcessContinuationFilter>();
        return builder;
    }
}
