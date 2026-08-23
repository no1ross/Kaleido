using Kaleido.Observability;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

public static class KaleidoServiceCollectionExtensions
{
    public static IKaleidoBuilder AddKaleido(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<KaleidoCorrelationContextAccessor>();
        services.AddScoped<IKaleidoCorrelationContextAccessor>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.AddScoped<IKaleidoCorrelationContextInitializer>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());

        return new KaleidoBuilder(services);
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
