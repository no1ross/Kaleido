using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.Http.Client;

/// <summary>Registers Process HTTP clients over a separately hydrated Registry participant.</summary>
public static class ProcessHttpClientServiceCollectionExtensions
{
    /// <summary>Installs the named gateway client and scoped Process factory without starting Registry participation.</summary>
    /// <param name="builder">The Kaleido host builder.</param>
    /// <param name="options">Shared gateway settings from code, overridden by configuration.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddProcessHttpClient(this IKaleidoBuilder builder, KaleidoClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(x => x.ServiceType == typeof(IKaleidoProcessorClientFactory)))
        {
            return builder;
        }

        builder.AddKaleidoHttpClientPipeline(options);
        var named = builder.AddKaleidoNamedClient(KaleidoProcessorClient.ClientName);
        var configured = builder.Services.Last(x => x.ServiceType == typeof(KaleidoClientOptions)).ImplementationInstance as KaleidoClientOptions;
        if (configured?.Resilience is { Enabled: true, MaxRetryAttempts: > 0 } resilience)
        {
            named.AddResilienceHandler("kaleido-process-keyed", (pipeline, context) =>
            {
                pipeline.TimeProvider = context.ServiceProvider.GetRequiredService<TimeProvider>();
                pipeline.AddKeyedPostRetry(resilience);
            });
        }

        builder.Services.TryAddScoped<IKaleidoProcessorClientFactory, KaleidoProcessorClientFactory>();
        return builder;
    }
}
