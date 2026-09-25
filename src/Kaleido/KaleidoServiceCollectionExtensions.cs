using Kaleido.Queryable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoServiceCollectionExtensions
{
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KaleidoServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceOptions = new KaleidoServiceOptions();
        configuration.GetSection(KaleidoServiceOptions.SectionName).Bind(serviceOptions);

        configure?.Invoke(serviceOptions);
        KaleidoServiceOptions.Validate(serviceOptions);
        services.AddSingleton(serviceOptions);

        // TryAdd-based: registers ILoggerFactory/ILogger<T> when the host hasn't —
        // provider configuration via ILoggingBuilder still applies normally.
        services.AddLogging();

        services.AddScoped<KaleidoCorrelationContextAccessor>();
        services.TryAddScoped<IKaleidoCorrelationContextAccessor>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddScoped<IKaleidoCorrelationContextInitializer>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddSingleton<IEventPublisher, NullEventPublisher>();
        services.TryAddSingleton<IDataTypeMapper, DataTypeMapper>();
        services.TryAddSingleton<IConstraintMapper, ConstraintMapper>();
        services.TryAddSingleton<Kaleido.Json.IValueConverter, Kaleido.Json.ValueConverter>();

        var builder = new KaleidoBuilder(services, configuration, serviceOptions);

        // Automatically register Process and Queryable runtimes
        builder.AddProcessor();
        builder.AddQueryable();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IEventPublisher"/> implementation, replacing the default no-op publisher.
    /// <typeparamref name="TPublisher"/> is registered as a singleton.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddEventPublisher&lt;HttpEventPublisher&gt;();
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddEventPublisher<TPublisher>(this IKaleidoBuilder builder)
        where TPublisher : class, IEventPublisher
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll<IEventPublisher>();
        builder.Services.AddSingleton<IEventPublisher, TPublisher>();

        return builder;
    }
}
