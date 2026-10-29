using Kaleido.Authorization;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaleido;

/// <summary>
/// Registers the Kaleido core on an <see cref="IServiceCollection"/>: <see cref="AddKaleido"/> is the
/// entry point; the other methods extend the returned <see cref="IKaleidoBuilder"/> - events
/// (<see cref="AddEventPublisher{TPublisher}"/>, <see cref="AddEventRedactor{TRedactor}"/>, <see cref="AddEventRedaction"/>), the stores
/// (<see cref="AddContextStore{TStore}"/>, <see cref="AddIdempotencyStore{TStore}"/>), the outbox
/// (<see cref="AddOutboxStore{TStore}"/>, <see cref="AddOutbox{TOutbox}"/>,
/// <see cref="AddDirectPublication"/>) and <see cref="AddTransactions{TTransactions}"/>.
/// </summary>
public static class KaleidoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kaleido core and the Process and Queryable runtimes for the steps, query sources
    /// and views found in <see cref="KaleidoServiceOptions.Assemblies"/>.
    /// </summary>
    /// <remarks>
    /// The <c>Kaleido</c> configuration section is laid over a copy of <paramref name="options"/>:
    /// a setting configuration names replaces the value from code, every other setting keeps it, and
    /// <paramref name="options"/> itself is not changed (ADR 0030). The result is validated - nothing is
    /// trimmed or substituted, an invalid value stops startup - and registered as the
    /// <see cref="KaleidoServiceOptions"/> singleton. Besides the runtimes it registers logging, the
    /// scoped correlation context accessor, and defaults the builder methods replace: a no-op
    /// <see cref="IEventPublisher"/> (<see cref="AddEventPublisher{TPublisher}"/>), in-memory stores
    /// (<see cref="IKaleidoContextStore"/>, <see cref="IKaleidoIdempotencyStore"/>), Kaleido's built-in
    /// outbox (<see cref="IKaleidoOutbox"/>) on an in-memory <see cref="IKaleidoOutboxStore"/> with its
    /// relay, and no transactions (<see cref="IKaleidoTransactions"/>).
    /// Transports and providers are added on the returned builder (e.g. <c>AddHttp</c>).
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">
    /// The application configuration; the <c>Kaleido</c> section overrides <paramref name="options"/>, and
    /// the configuration flows to every subsystem through <see cref="IKaleidoBuilder.Configuration"/>.
    /// </param>
    /// <param name="options">
    /// The options set in code, e.g. <c>ServiceName</c> and <c>Assemblies</c> (which only code can set);
    /// settings left out keep their defaults. <see langword="null"/> uses the defaults.
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
    /// builder.Services.AddKaleido(builder.Configuration, new()
    /// {
    ///     ServiceName = "intake",
    ///     Assemblies = [typeof(Program).Assembly],
    /// });
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration,
        KaleidoServiceOptions? options = null)
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

        // Configuration over code, on a copy (ADR 0030); validated here, at composition, rather than at
        // first resolution. The registered options hold their own copies of the assemblies and roles.
        var serviceOptions = configuration
            .GetSection(KaleidoServiceOptions.SectionName)
            .BindOver(options ?? new KaleidoServiceOptions(), ConfigurationErrorCodes.InvalidOptions);
        KaleidoServiceOptions.Validate(serviceOptions);
        serviceOptions = serviceOptions with
        {
            Assemblies = Array.AsReadOnly([.. serviceOptions.Assemblies ?? []]),
            DefaultAuthorization = serviceOptions.DefaultAuthorization with { Roles = [.. serviceOptions.DefaultAuthorization.Roles] },
        };
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

        // The clock of every lease, expiry, retry, TTL and circuit decision (#460); a host (or a
        // test) that registers its own TimeProvider first keeps it.
        services.TryAddSingleton(TimeProvider.System);

        AddStores(services);
        AddEventWriting(services);
        services.TryAddSingleton<IKaleidoAuthorizationEvaluator, KaleidoAuthorizationEvaluator>();
        services.TryAddSingleton<ITypeDescriber, TypeDescriber>();
        services.TryAddSingleton<IConstraintMapper, ConstraintMapper>();

        var builder = new KaleidoBuilder(services, configuration, serviceOptions);

        // Automatically register Process and Queryable runtimes
        builder.AddProcessor();
        builder.AddQueryable();

        return builder;
    }

    // The stores, the transactions and the outbox integration (ADR 0023), each replaced on the
    // builder: in memory, no transaction, and Kaleido's built-in outbox - its store and the relay that
    // hands the entries to the publisher.
    private static void AddStores(IServiceCollection services)
    {
        services.TryAddSingleton<IKaleidoContextStore, InMemoryKaleidoContextStore>();
        services.TryAddSingleton<IKaleidoIdempotencyStore, InMemoryKaleidoIdempotencyStore>();
        services.TryAddSingleton<IKaleidoTransactions, NoKaleidoTransactions>();
        services.TryAddScoped<IKaleidoOutbox, KaleidoStoreOutbox>();
        services.TryAddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();

        // At start, before the relay: every store in use is chosen - durable, or AddInMemoryStores()
        // (ADR 0028). It reads this collection as the host left it, so the Add* order does not matter.
        services.AddHostedService(_ => new KaleidoStoreSelectionCheck(services));
        services.AddHostedService<KaleidoEventRelay>();
    }

    // How events are written: the relay's wake-up signal, the event redaction policy (empty until
    // AddEventRedaction, ADR 0025), the payload writer that applies it and the per-request writer.
    private static void AddEventWriting(IServiceCollection services)
    {
        services.TryAddSingleton<IKaleidoEventSignal, KaleidoEventSignal>();
        services.TryAddSingleton(new KaleidoEventRedactionMap());
        services.TryAddSingleton<IKaleidoEventRedactorProvider, KaleidoEventRedactorProvider>();
        services.TryAddSingleton<IKaleidoEventPayloadWriter, KaleidoEventPayloadWriter>();
        services.TryAddScoped<IKaleidoEventWriter, KaleidoEventWriter>();
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
    /// For classified properties (<c>DataClassificationAttribute</c>) no redactor is needed: choose how
    /// each classification is redacted with <see cref="AddEventRedaction"/>. See
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

    /// <summary>
    /// Chooses how classified values (<c>DataClassificationAttribute</c>, e.g. <c>[Phi]</c>, <c>[Pii]</c>)
    /// are redacted in event payloads, independently of log redaction: the host's <c>AddRedaction</c>
    /// policy applies to logs only, this one to events only (ADR 0025).
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="configure">
    /// Maps sets of classifications to redactors with the standard <see cref="IRedactionBuilder"/> API,
    /// so Microsoft's extensions (e.g. <c>SetHmacRedactor</c>) work in it.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// A classified value gets the redactor of its exact set of classifications, else the fallback
    /// (<see cref="IRedactionBuilder.SetFallbackRedactor{T}"/>); without either it is erased. Without
    /// <c>AddEventRedaction</c> every classified value is erased, and a warning says so once.
    /// </para>
    /// <para>
    /// Keep a classification unredacted in events with <see cref="NullRedactor"/>. Redactor types are
    /// registered as singletons. Calling it again adds to the same policy.
    /// </para>
    /// </remarks>
    /// <example>
    /// Logs never carry PHI or PII; events keep PII and erase PHI:
    /// <code>
    /// builder.Services.AddRedaction(r =&gt; r.SetRedactor&lt;ErasingRedactor&gt;(new(Phi), new(Pii)));   // logs
    ///
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddEventPublisher&lt;WarehousePublisher&gt;()
    ///     .AddEventRedaction(r =&gt; r.SetRedactor&lt;NullRedactor&gt;(new DataClassificationSet(Pii)));  // events: PHI erased (no redactor)
    /// </code>
    /// </example>
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
    /// Registers the process-state store, replacing the in-memory default. <typeparamref name="TStore"/>
    /// is registered as scoped (it may share a scoped <c>DbContext</c> with the outbox).
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingStore"/>: another context store was registered
    /// (e.g. by a provider).
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddContextStore&lt;OrdersContextStore&gt;();
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddContextStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoContextStore =>
        Replace<IKaleidoContextStore, TStore, InMemoryKaleidoContextStore>(builder, "context store");

    /// <summary>
    /// Registers the store of idempotent requests, replacing the in-memory default. It needs no shared
    /// transaction, so it may live in any database. <typeparamref name="TStore"/> is registered as scoped.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingStore"/>: another idempotency store was registered.
    /// </exception>
    public static IKaleidoBuilder AddIdempotencyStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoIdempotencyStore =>
        Replace<IKaleidoIdempotencyStore, TStore, InMemoryKaleidoIdempotencyStore>(builder, "idempotency store");

    /// <summary>
    /// Chooses Kaleido's in-memory stores, explicitly, for every store the service uses and has not
    /// registered otherwise: process state, idempotency records and the event outbox are kept in this
    /// process only - lost on restart, and not shared between instances. For development, tests and
    /// demos.
    /// </summary>
    /// <remarks>
    /// Without a choice, the host does not start when a store the service uses is still the in-memory
    /// default (<see cref="ConfigurationErrorCodes.StoresNotChosen"/>, ADR 0028): the context and
    /// idempotency stores when the service has process steps, and the outbox store when events are
    /// published through Kaleido's built-in outbox. A query-only service, the host's own outbox
    /// (<see cref="AddOutbox{TOutbox}"/>) and direct publication (<see cref="AddDirectPublication"/>)
    /// need no outbox store. Durable stores registered alongside (e.g. <c>AddSqliteContextStore</c>)
    /// are kept; the in-memory stores still log a Warning when created.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration, new() { /* ... */ })
    ///     .AddHttp()
    ///     .AddInMemoryStores();   // local development: nothing survives a restart
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddInMemoryStores(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<KaleidoInMemoryStoresChoice>();

        return builder;
    }

    /// <summary>
    /// Registers how process-state transitions become atomic for your persistence (e.g. a transaction on
    /// the <c>DbContext</c> your context store and outbox write to). Optional: without it each store
    /// write stands alone. <typeparamref name="TTransactions"/> is registered as scoped.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingStore"/>: other transactions were registered (e.g.
    /// by <c>AddSqliteContextStore</c>): transactions belong to the persistence of the process state.
    /// </exception>
    public static IKaleidoBuilder AddTransactions<TTransactions>(this IKaleidoBuilder builder)
        where TTransactions : class, IKaleidoTransactions =>
        Replace<IKaleidoTransactions, TTransactions, NoKaleidoTransactions>(builder, "transactions");

    /// <summary>
    /// Stores Kaleido's built-in outbox in <typeparamref name="TStore"/> instead of memory; Kaleido's
    /// relay still claims, retries and publishes the entries through <see cref="IEventPublisher"/>.
    /// Registered as scoped. Atomic with the process state when both are on one database covered by
    /// <see cref="IKaleidoTransactions"/>.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingOutbox"/>: the service uses its own outbox or direct
    /// publication, or another outbox store was registered.
    /// </exception>
    public static IKaleidoBuilder AddOutboxStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoOutboxStore
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (RegisteredType<IKaleidoOutbox>(builder.Services) is { } outbox && outbox != typeof(KaleidoStoreOutbox))
        {
            ThrowConflictingOutbox($"AddOutboxStore<{typeof(TStore).Name}>() stores Kaleido's built-in outbox, but the service already uses {outbox.Name}.");
        }

        return Replace<IKaleidoOutboxStore, TStore, InMemoryKaleidoOutboxStore>(builder, "outbox store", ConfigurationErrorCodes.ConflictingOutbox);
    }

    /// <summary>
    /// Uses the host's own outbox instead of Kaleido's: <typeparamref name="TOutbox"/> receives every
    /// event (<see cref="IKaleidoOutbox"/>) and its outbox delivers them. Kaleido's outbox store and relay
    /// are removed, and no <see cref="IEventPublisher"/> is needed. Registered as scoped.
    /// </summary>
    /// <remarks>
    /// State and events are atomic when <typeparamref name="TOutbox"/> writes to the database of the
    /// process state inside the transaction of <see cref="IKaleidoTransactions"/> (e.g. the same
    /// <c>DbContext</c>, or a provider's transaction it joins).
    /// </remarks>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingOutbox"/>: another outbox choice (an outbox store,
    /// another outbox, or direct publication) was registered.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddContextStore&lt;OrdersContextStore&gt;()
    ///     .AddOutbox&lt;OrdersOutbox&gt;()
    ///     .AddTransactions&lt;OrdersTransactions&gt;();
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddOutbox<TOutbox>(this IKaleidoBuilder builder)
        where TOutbox : class, IKaleidoOutbox =>
        ReplaceOutbox(builder, typeof(TOutbox), $"AddOutbox<{typeof(TOutbox).Name}>()");

    /// <summary>
    /// Publishes events directly instead of through an outbox: once a transition is committed, its
    /// events are handed to <see cref="IEventPublisher"/> once. Weaker by choice - an event is lost when
    /// publishing fails or the process stops first; there is no retry. Kaleido's outbox store and relay
    /// are removed.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingOutbox"/>: another outbox choice was registered.
    /// </exception>
    public static IKaleidoBuilder AddDirectPublication(this IKaleidoBuilder builder) =>
        ReplaceOutbox(builder, typeof(KaleidoDirectPublicationOutbox), "AddDirectPublication()");

    // One outbox choice per service: the built-in outbox (default, or with an outbox store), the host's
    // own, or direct publication.
    private static IKaleidoBuilder ReplaceOutbox(IKaleidoBuilder builder, Type outbox, string call)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        if (RegisteredType<IKaleidoOutbox>(services) is { } registered && registered != typeof(KaleidoStoreOutbox))
        {
            ThrowConflictingOutbox($"{call} replaces Kaleido's outbox, but the service already uses {registered.Name}.");
        }

        if (RegisteredType<IKaleidoOutboxStore>(services) is { } store && store != typeof(InMemoryKaleidoOutboxStore))
        {
            ThrowConflictingOutbox($"{call} replaces Kaleido's outbox, but an outbox store ({store.Name}) was registered for it.");
        }

        services.RemoveAll<IKaleidoOutbox>();
        services.RemoveAll<IKaleidoOutboxStore>();
        foreach (var relay in services.Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(KaleidoEventRelay)).ToList())
        {
            services.Remove(relay);
        }

        services.AddScoped(typeof(IKaleidoOutbox), outbox);

        return builder;
    }

    // Replaces the default; the same implementation again is a no-op, a different one a conflict.
    private static IKaleidoBuilder Replace<TService, TImplementation, TDefault>(
        IKaleidoBuilder builder,
        string what,
        string code = ConfigurationErrorCodes.ConflictingStore)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);

        var registered = RegisteredType<TService>(builder.Services);

        if (registered == typeof(TImplementation))
        {
            return builder;
        }

        if (registered is not null && registered != typeof(TDefault))
        {
            throw new KaleidoConfigurationException(
                code,
                $"Two {what} implementations were registered: {registered.Name} and {typeof(TImplementation).Name}. A service has one {what}.");
        }

        builder.Services.RemoveAll<TService>();
        builder.Services.AddScoped<TService, TImplementation>();

        return builder;
    }

    // The implementation registered for a service: its type, an instance's type, or - for a factory,
    // whose type is unknown until resolved - the service type itself (never a default).
    private static Type? RegisteredType<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService)) is { } registration
            ? registration.ImplementationType ?? registration.ImplementationInstance?.GetType() ?? typeof(TService)
            : null;

    [DoesNotReturn]
    private static void ThrowConflictingOutbox(string message) =>
        throw new KaleidoConfigurationException(
            ConfigurationErrorCodes.ConflictingOutbox,
            message + " A service has one outbox: Kaleido's (with one outbox store), its own (AddOutbox), or direct publication (AddDirectPublication).");
}
