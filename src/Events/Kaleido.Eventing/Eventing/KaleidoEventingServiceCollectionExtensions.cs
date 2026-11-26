using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Eventing;

/// <summary>Registers the existing Eventing writer, outbox, redaction, health check and relay.</summary>
public static class KaleidoEventingServiceCollectionExtensions
{
    /// <summary>Registers Eventing's outbox-backed writer and binds its options from <c>Kaleido:Events</c>.</summary>
    /// <remarks>
    /// Configuration overrides a copy of <paramref name="options"/> setting by setting, keeping the
    /// existing <c>Kaleido:Events</c> keys and validating the effective values before registration.
    /// A provider must register an outbox store; <c>AddInMemoryProviders()</c> does so explicitly
    /// for development, while production hosts choose durable storage. No publisher or relay is registered: use
    /// <see cref="AddOutboxRelay{TPublisher}"/> on the host that drains the outbox.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="options">The Eventing options from code, or defaults when omitted.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.InvalidOptions"/> for an unreadable configured value,
    /// <see cref="ConfigurationErrorCodes.InvalidEventOptions"/> for invalid effective settings, or
    /// <see cref="ConfigurationErrorCodes.DuplicateRegistration"/> for a second registration.
    /// </exception>
    public static IKaleidoBuilder AddEventing(this IKaleidoBuilder builder, KaleidoEventOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        if (services.Any(x => x.ServiceType == typeof(KaleidoEventOptions)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.DuplicateRegistration, "AddEventing() was called more than once on this service collection.");
        }

        var events = builder.BindOptions(options);
        services.AddSingleton(events);

        // Eventing wraps the host's explicitly registered outbox store (ADR 0023, #552); the host
        // that publishes adds the relay separately.
        services.TryAddScoped<IKaleidoOutbox, KaleidoStoreOutbox>();
        // The relay's wake-up signal, event redaction policy (empty until AddEventRedaction,
        // ADR 0025), payload writer and per-request writer make up event writing.
        services.TryAddSingleton<IKaleidoEventSignal, KaleidoEventSignal>();
        services.TryAddSingleton(new KaleidoEventRedactionMap());
        services.TryAddSingleton<IKaleidoEventRedactorProvider, KaleidoEventRedactorProvider>();
        services.TryAddSingleton<IKaleidoEventPayloadWriter, KaleidoEventPayloadWriter>();
        services.TryAddScoped<IKaleidoEventWriter, KaleidoEventWriter>();
        // Every Eventing host watches its own outbox for a recent relay and backlog (#552).
        // The check resolves a scoped store, and AddEventing runs once so it is registered once.
        services.AddHealthChecks().AddCheck<KaleidoOutboxHealthCheck>(KaleidoOutboxHealthCheck.Name, failureStatus: null, tags: ["kaleido", "outbox"]);

        return builder;
    }

    /// <summary>
    /// Runs one outbox relay in this host, claiming every event in the shared outbox database and
    /// publishing through <typeparamref name="TPublisher"/> at least once. Failures are retried with
    /// backoff until parked (#552).
    /// </summary>
    /// <remarks>
    /// A service writing events needs no publisher. The relay can run in one of those services or
    /// in a host with no Process/Queryable runtime; such a host registers the typed dispatchers for
    /// the event families in its database separately. Several relay instances coordinate through
    /// leased <see cref="IKaleidoOutboxStore.ClaimEventsAsync"/> calls, each taking up to
    /// <see cref="KaleidoEventOptions.BatchSize"/> entries. A database no relay reads keeps growing.
    /// </remarks>
    /// <typeparam name="TPublisher">The event publisher, registered as a singleton unless already supplied by the host.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Eventing is not registered or another relay publisher is registered.</exception>
    public static IKaleidoBuilder AddOutboxRelay<TPublisher>(this IKaleidoBuilder builder)
        where TPublisher : class, IEventPublisher
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        if (!services.Any(x => x.ServiceType == typeof(KaleidoEventOptions)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidEventOptions, "Call AddEventing() before AddOutboxRelay().");
        }

        if (services.LastOrDefault(x => x.ServiceType == typeof(KaleidoOutboxRelayRegistration))?.ImplementationInstance is KaleidoOutboxRelayRegistration registered)
        {
            return registered.Publisher == typeof(TPublisher)
                ? builder
                : throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ConflictingOutbox,
                    $"Two outbox relays were registered: publishing through {registered.Publisher.Name} and {typeof(TPublisher).Name}. A host runs one relay, with one publisher.");
        }

        services.AddSingleton(new KaleidoOutboxRelayRegistration(typeof(TPublisher)));
        services.TryAddSingleton<TPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<TPublisher>());
        services.AddHostedService<KaleidoEventRelay>();

        return builder;
    }

    /// <summary>
    /// Registers an <see cref="IKaleidoEventRedactor"/> singleton that reduces every event before
    /// the outbox write. Several redactors run in registration order; classified properties require
    /// no custom redactor and are controlled independently with <see cref="AddEventRedaction"/>.
    /// </summary>
    /// <typeparam name="TRedactor">The event redactor implementation.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddEventRedactor<TRedactor>(this IKaleidoBuilder builder)
        where TRedactor : class, IKaleidoEventRedactor
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IKaleidoEventRedactor, TRedactor>();

        return builder;
    }

    /// <summary>Sets Eventing's classified-payload redaction policy independently of host log redaction (ADR 0025).</summary>
    /// <remarks>
    /// Exact classification sets use their mapped redactor, otherwise the fallback; without either
    /// the value is erased. Without <c>AddEventRedaction</c> every classified value is erased and a
    /// warning is logged once. Use <see cref="NullRedactor"/> to keep selected classifications in
    /// events; the host's <c>AddRedaction</c> policy affects logs only. Repeated calls extend the same
    /// event policy and redactor types are registered as singletons.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="configure">Configures classification redactors through the standard <see cref="IRedactionBuilder"/> API.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddEventRedaction(this IKaleidoBuilder builder, Action<IRedactionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var map = builder.Services.LastOrDefault(x => x.ServiceType == typeof(KaleidoEventRedactionMap))?.ImplementationInstance as KaleidoEventRedactionMap;

        if (map is null)
        {
            map = new KaleidoEventRedactionMap();
            builder.Services.AddSingleton(map);
        }

        map.IsConfigured = true;
        configure(new KaleidoEventRedactionBuilder(builder.Services, map));

        return builder;
    }

    /// <summary>
    /// Registers one scoped implementation of <see cref="IKaleidoOutboxStore"/> in place of the
    /// chosen in-memory registration, if present. Keep the outbox on the same database as Process state when a host's
    /// <c>IKaleidoTransactions</c> must commit state changes and events atomically; a relay on that
    /// database publishes the entries.
    /// </summary>
    /// <typeparam name="TStore">The outbox store implementation.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Another outbox store was chosen.</exception>
    public static IKaleidoBuilder AddOutboxStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoOutboxStore =>
        builder.Replace<IKaleidoOutboxStore, TStore, TStore>("outbox store", ConfigurationErrorCodes.ConflictingOutbox);
}
