using Kaleido.Eventing;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido;

public static class KaleidoServiceCollectionExtensions
{
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<KaleidoCorrelationContextAccessor>();
        services.TryAddScoped<IKaleidoCorrelationContextAccessor>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddScoped<IKaleidoCorrelationContextInitializer>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddSingleton<IEventPublisher, NullEventPublisher>();

        return new KaleidoBuilder(services, configuration);
    }

    public static IKaleidoBuilder AddAssembly(this IKaleidoBuilder builder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);

        if (builder is KaleidoBuilder kaleidoBuilder) 
        { 
            kaleidoBuilder.AddAssembly(assembly); 
        }

        return builder;
    }
}
