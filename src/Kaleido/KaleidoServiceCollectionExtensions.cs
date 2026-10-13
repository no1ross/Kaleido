using Kaleido.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoServiceCollectionExtensions
{
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KaleidoServiceOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind + configure run on the mutable builder; the immutable
        // KaleidoServiceOptions snapshot is what DI sees (EXT-16). Eager
        // validation here is strictly stronger than ValidateOnStart — it
        // fails at composition, not first resolution.
        var optionsBuilder = new KaleidoServiceOptionsBuilder();
        configuration.GetSection(KaleidoServiceOptions.SectionName).Bind(optionsBuilder);

        configure?.Invoke(optionsBuilder);
        var serviceOptions = optionsBuilder.Build();
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
        services.TryAddSingleton<IEventPublisher, EventPublisher>();

        // One store per service (process state, idempotent results, event outbox) and the relay
        // that hands outbox entries to the publisher.
        services.TryAddSingleton<IKaleidoContextStore, InMemoryKaleidoContextStore>();
        services.TryAddSingleton<IKaleidoEventSignal, KaleidoEventSignal>();
        services.TryAddSingleton<IKaleidoEventPayloadWriter, KaleidoEventPayloadWriter>();
        services.TryAddScoped<IKaleidoEventOutbox, KaleidoEventOutbox>();
        services.AddHostedService<KaleidoEventRelay>();
        services.TryAddSingleton<IKaleidoAuthorizationEvaluator, KaleidoAuthorizationEvaluator>();
        services.TryAddSingleton<ITypeDescriber, TypeDescriber>();
        services.TryAddSingleton<IConstraintMapper, ConstraintMapper>();

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

    /// <summary>
    /// Registers an <see cref="IKaleidoEventRedactor"/> that reduces every event before it is written
    /// to the outbox. Several redactors run in registration order; <typeparamref name="TRedactor"/> is
    /// registered as a singleton.
    /// </summary>
    /// <remarks>
    /// For classified properties (<c>DataClassificationAttribute</c>) no redactor is needed: register
    /// <c>AddRedaction</c> to choose how each classification is redacted. See
    /// <see cref="IKaleidoEventRedactor"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddEventPublisher&lt;HttpEventPublisher&gt;()
    ///     .AddEventRedactor&lt;AnswersRedactor&gt;();
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddEventRedactor<TRedactor>(this IKaleidoBuilder builder)
        where TRedactor : class, IKaleidoEventRedactor
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IKaleidoEventRedactor, TRedactor>();

        return builder;
    }
}
