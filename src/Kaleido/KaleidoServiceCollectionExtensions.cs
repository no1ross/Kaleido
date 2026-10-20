using Kaleido.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

/// <summary>
/// Registers the Kaleido core on an <see cref="IServiceCollection"/>: <see cref="AddKaleido"/> is the
/// entry point; <see cref="AddEventPublisher{TPublisher}"/> and
/// <see cref="AddEventRedactor{TRedactor}"/> extend the returned <see cref="IKaleidoBuilder"/>.
/// </summary>
public static class KaleidoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kaleido core and the Process and Queryable runtimes for the steps, query sources
    /// and views found in <see cref="KaleidoServiceOptions.Assemblies"/>.
    /// </summary>
    /// <remarks>
    /// Options are bound from the <c>Kaleido</c> configuration section, then <paramref name="configure"/>
    /// runs, then the result is validated and registered as an immutable
    /// <see cref="KaleidoServiceOptions"/> singleton. Besides the runtimes it registers logging, the
    /// scoped correlation context accessor, the event outbox and relay, and defaults the host can
    /// replace by registering its own first: a no-op <see cref="IEventPublisher"/> (replace it with
    /// <see cref="AddEventPublisher{TPublisher}"/>) and in-memory stores
    /// (<see cref="IKaleidoContextStore"/>, <see cref="IKaleidoOutboxStore"/>,
    /// <see cref="IKaleidoIdempotencyStore"/>).
    /// Transports and providers are added on the returned builder (e.g. <c>AddHttp</c>).
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">
    /// The application configuration; the <c>Kaleido</c> section is bound to the options and the
    /// configuration flows to every subsystem through <see cref="IKaleidoBuilder.Configuration"/>.
    /// </param>
    /// <param name="configure">
    /// Optionally sets options in code after configuration binding, e.g. <c>ServiceName</c> and
    /// <c>Assemblies</c>.
    /// </param>
    /// <returns>The builder for chaining further Kaleido registrations.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KaleidoConfigurationException">
    /// The options are invalid: e.g. a missing or invalid <see cref="KaleidoServiceOptions.ServiceName"/>,
    /// no <see cref="KaleidoServiceOptions.Assemblies"/>, or invalid authorization or event settings.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration, o =>
    /// {
    ///     o.ServiceName = "intake";
    ///     o.Assemblies = [typeof(Program).Assembly];
    /// });
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KaleidoServiceOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // One Kaleido service per host: a second call would silently replace the first one's options
        // (its service name, assemblies, authorization), so it is refused (#301).
        if (services.Any(x => x.ServiceType == typeof(KaleidoServiceOptions)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.DuplicateRegistration,
                "AddKaleido() was called more than once on this service collection. A host runs one Kaleido service: configure it in a single AddKaleido() call.");
        }

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
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>(); // ADR 0022
        services.TryAddSingleton<IEventPublisher, EventPublisher>();

        // The three stores (process state, event outbox, idempotent requests) and the relay that hands
        // outbox entries to the publisher. The in-memory context store adds a save's events to the
        // registered outbox.
        services.TryAddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();
        services.TryAddSingleton<IKaleidoContextStore, InMemoryKaleidoContextStore>();
        services.TryAddSingleton<IKaleidoIdempotencyStore, InMemoryKaleidoIdempotencyStore>();
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
