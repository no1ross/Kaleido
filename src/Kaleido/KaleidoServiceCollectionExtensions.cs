using Kaleido.Authorization;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

/// <summary>
/// Registers the Kaleido core on an <see cref="IServiceCollection"/>: <see cref="AddKaleido"/> is the
/// entry point; the other methods extend the returned <see cref="IKaleidoBuilder"/> - events
/// (<see cref="AddEventRedactor{TRedactor}"/>, <see cref="AddEventRedaction"/>), the stores
/// (<see cref="AddContextStore{TStore}"/>, <see cref="AddIdempotencyStore{TStore}"/>,
/// <see cref="AddOutboxStore{TStore}"/>), <see cref="AddTransactions{TTransactions}"/> and the relay that
/// publishes the outbox (<see cref="AddOutboxRelay{TPublisher}"/>).
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
    /// scoped correlation context accessor, and defaults the builder methods replace: in-memory stores
    /// (<see cref="IKaleidoContextStore"/>, <see cref="IKaleidoIdempotencyStore"/>,
    /// <see cref="IKaleidoOutboxStore"/>) and no transactions (<see cref="IKaleidoTransactions"/>). Every
    /// service writes its events to the outbox store; the host that publishes them adds
    /// <see cref="AddOutboxRelay{TPublisher}"/> (#552).
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

    // The stores, the transactions and the outbox (ADR 0023, #552), each store replaced on the builder:
    // in memory and no transaction by default. The relay is not here: the host that publishes adds it
    // (AddOutboxRelay).
    private static void AddStores(IServiceCollection services)
    {
        services.TryAddSingleton<IKaleidoContextStore, InMemoryKaleidoContextStore>();
        services.TryAddSingleton<IKaleidoIdempotencyStore, InMemoryKaleidoIdempotencyStore>();
        services.TryAddSingleton<IKaleidoTransactions, NoKaleidoTransactions>();
        services.TryAddScoped<IKaleidoOutbox, KaleidoStoreOutbox>();
        services.TryAddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();

        // At start, before a relay: every store in use is chosen - durable, or AddInMemoryStores()
        // (ADR 0028). It reads this collection as the host left it, so the Add* order does not matter.
        services.AddHostedService(_ => new KaleidoStoreSelectionCheck(services));
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
    /// Runs Kaleido's outbox relay in this host, publishing through <typeparamref name="TPublisher"/>: it
    /// claims every event in the outbox store's database - this service's and those of every other
    /// service writing to it - hands each to <typeparamref name="TPublisher"/>, and retries a failed one
    /// with backoff until it parks it (#552). The relay's settings are <see cref="KaleidoServiceOptions.Events"/>.
    /// </summary>
    /// <typeparam name="TPublisher">
    /// The publisher, registered as a singleton unless the host registered <typeparamref name="TPublisher"/>
    /// itself (then that registration is used).
    /// </typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Every service writes its events to its outbox store, in the same transaction as its state; it needs
    /// no publisher. The relay may run in one of them, on that service's database, or in a host with
    /// nothing else (no steps or queries) on a database several services share. Several relay instances
    /// coordinate through leased claims (<see cref="IKaleidoOutboxStore.ClaimEventsAsync"/>), each taking up
    /// to <see cref="KaleidoEventOptions.BatchSize"/> entries at a time.
    /// </para>
    /// <para>
    /// Something must drain every outbox: a database no relay reads keeps growing.
    /// </para>
    /// </remarks>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingOutbox"/>: a relay with another publisher was
    /// registered. A host runs one relay, with one publisher.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration, new() { ServiceName = "outbox-relay", Assemblies = [typeof(Program).Assembly], TypeFilter = _ =&gt; false })
    ///     .AddSqliteOutboxStore(sharedConnectionString)
    ///     .AddOutboxRelay&lt;WarehousePublisher&gt;();
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddOutboxRelay<TPublisher>(this IKaleidoBuilder builder)
        where TPublisher : class, IEventPublisher
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

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
    /// idempotency stores when the service has process steps, and the outbox store always: every service
    /// writes its events, a query-only service included (#552). Durable stores registered alongside (e.g.
    /// <c>AddSqliteContextStore</c>) are kept; the in-memory stores still log a Warning when created. An
    /// in-memory outbox is drained only by a relay in the same process (<see cref="AddOutboxRelay{TPublisher}"/>).
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
    /// Stores the service's outbox - every event it writes - in <typeparamref name="TStore"/> instead of
    /// memory. Registered as scoped. Atomic with the process state when both are on one database covered by
    /// <see cref="IKaleidoTransactions"/>: keep the outbox on the context store's database. The relay
    /// (<see cref="AddOutboxRelay{TPublisher}"/>, here or in another host on that database) publishes the
    /// entries.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingOutbox"/>: another outbox store was registered.
    /// </exception>
    public static IKaleidoBuilder AddOutboxStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoOutboxStore =>
        Replace<IKaleidoOutboxStore, TStore, InMemoryKaleidoOutboxStore>(builder, "outbox store", ConfigurationErrorCodes.ConflictingOutbox);
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
}
