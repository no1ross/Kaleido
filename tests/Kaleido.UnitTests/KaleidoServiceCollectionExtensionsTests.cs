using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceCollectionExtensionsTests
    : SutFixture
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    // Provides valid bootstrap options so unrelated DI tests pass startup validation.
    private static KaleidoServiceOptions DefaultServiceOptions() =>
        new()
        {
            ServiceName = "test-service",
            Assemblies = [typeof(KaleidoServiceCollectionExtensionsTests).Assembly],
        };

    // Three store contracts, one per concern, each with an in-memory default the host can replace.
    [Fact]
    public void AddKaleido_RegistersTheThreeInMemoryStores_AndAHostsOwnStoreWins()
    {
        var services = new ServiceCollection();
        var ownOutbox = Mock.Of<Kaleido.Storage.IKaleidoOutboxStore>();
        services.AddSingleton(ownOutbox);

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.Equal("InMemoryKaleidoContextStore", Single<Kaleido.Storage.IKaleidoContextStore>(services).ImplementationType?.Name);
        Assert.Equal("InMemoryKaleidoIdempotencyStore", Single<Kaleido.Storage.IKaleidoIdempotencyStore>(services).ImplementationType?.Name);
        Assert.Same(ownOutbox, Single<Kaleido.Storage.IKaleidoOutboxStore>(services).ImplementationInstance);
    }

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    // ADR 0030: configuration overrides code setting by setting; what configuration does not name keeps
    // the code value, and the host's own instance is never changed.
    [Fact]
    public void AddKaleido_LaysConfigurationOverTheOptionsFromCode_WithoutChangingThem()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DisplayName"] = "From configuration",
                ["Kaleido:Events:BatchSize"] = "7",
                ["Kaleido:DefaultAuthorization:Roles:0"] = "radiology",
            })
            .Build();
        var code = DefaultServiceOptions() with
        {
            DisplayName = "From code",
            Description = "Kept",
            Events = new() { BatchSize = 3, MaxConcurrentPublishes = 2 },
            DefaultAuthorization = new AuthorizationMetadata("staff", ["admin"]),
        };

        var registered = new ServiceCollection().AddKaleido(config, code).ServiceOptions;

        Assert.Equal("From configuration", registered.DisplayName);
        Assert.Equal("Kept", registered.Description);
        Assert.Equal(7, registered.Events.BatchSize);
        Assert.Equal(2, registered.Events.MaxConcurrentPublishes);
        Assert.Equal("staff", registered.DefaultAuthorization.Policy);
        Assert.Equal(["radiology"], registered.DefaultAuthorization.Roles); // replaced, not appended
        Assert.Equal("From code", code.DisplayName);
        Assert.Equal(3, code.Events.BatchSize);
        Assert.Equal(["admin"], code.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddKaleido_AConfiguredValueThatCannotBeRead_IsAConfigurationError()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Events:BatchSize"] = "many" })
            .Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => new ServiceCollection().AddKaleido(config, DefaultServiceOptions()));

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains("Kaleido:Events:BatchSize", exception.Message, StringComparison.Ordinal);
    }

    // The registered options hold their own copies: changing the caller's array or list afterwards
    // changes nothing.
    [Fact]
    public void AddKaleido_RegistersCopiesOfTheAssembliesAndRoles()
    {
        var assemblies = new[] { typeof(KaleidoServiceOptions).Assembly };
        var roles = new List<string> { "admin" };

        var registered = new ServiceCollection()
            .AddKaleido(EmptyConfig(), DefaultServiceOptions() with { Assemblies = assemblies, DefaultAuthorization = new AuthorizationMetadata(null, roles) })
            .ServiceOptions;
        assemblies[0] = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        roles.Add("other");

        Assert.Equal([typeof(KaleidoServiceOptions).Assembly], registered.Assemblies);
        Assert.Equal(["admin"], registered.DefaultAuthorization.Roles);
        Assert.Throws<NotSupportedException>(() => ((IList<System.Reflection.Assembly>)registered.Assemblies!)[0] = assemblies[0]);
    }

    [Fact]
    public void AddKaleido_WithoutEventOptions_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            new ServiceCollection().AddKaleido(EmptyConfig(), DefaultServiceOptions() with { Events = null! }));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
    }

    // #301: one Kaleido service per host; a second call would silently replace the first one's options.
    [Fact]
    public void AddKaleido_CalledTwice_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => services.AddKaleido(EmptyConfig(), DefaultServiceOptions()));

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    // #288: a host that does not choose gets Authenticated; leaving capabilities open is an explicit None.
    [Fact]
    public void AddKaleido_WithoutAnAuthorizationMode_EnforcesAuthenticatedCallers()
    {
        var services = new ServiceCollection();

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        var options = Assert.IsType<KaleidoServiceOptions>(Single<KaleidoServiceOptions>(services).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.Authenticated, options.AuthorizationMode);
    }

    [Fact]
    public void AddKaleido_WithAuthorizationModeNoneInConfiguration_LeavesCapabilitiesOpen()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:AuthorizationMode"] = "None" })
            .Build();

        services.AddKaleido(configuration, DefaultServiceOptions());

        var options = Assert.IsType<KaleidoServiceOptions>(Single<KaleidoServiceOptions>(services).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.None, options.AuthorizationMode);
    }

    [Fact]
    public void AddKaleido_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(
            () => services!.AddKaleido(EmptyConfig()));
    }

    [Fact]
    public void AddKaleido_ShouldThrow_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddKaleido(null!));
    }

    [Fact]
    public void AddKaleido_ShouldReturn_KaleidoBuilder()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.NotNull(builder);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        Assert.Same(
            services,
            concreteBuilder.Services);
    }

    [Fact]
    public void AddKaleido_WithAssembliesInOptions_RegistersAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Same(assembly, Assert.Single(concreteBuilder.Assemblies));
    }

    [Fact]
    public void AddKaleido_WithMultipleAssembliesInOptions_RegistersAllAssemblies()
    {
        var services = new ServiceCollection();
        var assembly1 = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        var assembly2 = typeof(IServiceCollection).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly1, assembly2 } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly1, concreteBuilder.Assemblies);
        Assert.Contains(assembly2, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithDuplicateAssembliesInOptions_DeduplicatesAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly, assembly } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Equal(1, concreteBuilder.Assemblies.Count(a => a == assembly));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddKaleido_WhenAssembliesAreMissingOrEmpty_FailsBeforeRegistration(bool empty)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = empty ? [] : null }));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void AddKaleido_NoServiceAuthorizationRule_UsesUnspecified()
    {
        var services = new ServiceCollection();

        var builder = services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.False(builder.ServiceOptions.DefaultAuthorization.IsExplicit());
        Assert.Empty(builder.ServiceOptions.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddKaleido_BindsServiceAuthorizationRuleFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DefaultAuthorization:Policy"] = "staff",
                ["Kaleido:DefaultAuthorization:Roles:0"] = "radiology"
            })
            .Build();
        var services = new ServiceCollection();

        var builder = services.AddKaleido(config, DefaultServiceOptions());

        Assert.Equal("staff", builder.ServiceOptions.DefaultAuthorization.Policy);
        Assert.Equal("radiology", Assert.Single(builder.ServiceOptions.DefaultAuthorization.Roles));
        Assert.False(AuthorizationMetadata.Unspecified.IsExplicit());
    }

    [Fact]
    public void AddKaleido_RejectsConflictingServiceAuthorizationRule()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new()
            {
                ServiceName = "test-service",
                DefaultAuthorization = new AuthorizationMetadata("staff", []) { AllowAnonymous = true },
            }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void AddKaleido_RejectsUnknownAuthorizationMode()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", AuthorizationMode = (KaleidoAuthorizationMode)123 }));

        Assert.Equal(ConfigurationErrorCodes.InvalidAuthorizationMode, exception.Code);
    }

    [Fact]
    public void AddKaleido_ShouldNotOverride_PreregisteredCorrelationContextAccessor()
    {
        var services = new ServiceCollection();
        var custom = new CustomCorrelationContextAccessor();
        services.AddScoped<IKaleidoCorrelationContextAccessor>(_ => custom);

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldNotOverride_PreregisteredCorrelationContextInitializer()
    {
        var services = new ServiceCollection();
        var custom = new CustomCorrelationContextInitializer();
        services.AddScoped<IKaleidoCorrelationContextInitializer>(_ => custom);

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultAccessor_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultInitializer_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddEventRedactor_RegistersEachRedactorInOrder()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions())
            .AddEventRedactor<FirstRedactor>()
            .AddEventRedactor<SecondRedactor>();

        using var provider = services.BuildServiceProvider(validateScopes: false);

        Assert.Equal(
            [typeof(FirstRedactor), typeof(SecondRedactor)],
            provider.GetServices<Kaleido.Eventing.IKaleidoEventRedactor>().Select(x => x.GetType()));
    }

    [Fact]
    public void AddEventRedactor_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddEventRedactor<FirstRedactor>());
    }

    [Fact]
    public void AddKaleido_WithoutConfigureCallback_UsesBoundConfigurationAndValidatesIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:ServiceName"] = "bound-service" })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => services.AddKaleido(config));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    // #552: no publisher and no relay by default - every service only writes its events.
    [Fact]
    public void AddKaleido_RegistersNoPublisherAndNoRelay()
    {
        var services = NewBuilder().Services;

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(Kaleido.Eventing.IEventPublisher));
        Assert.False(RelayRegistered(services));
    }

    // The relay owns the publisher: registered as a singleton, unless the host registered it itself.
    [Fact]
    public void AddOutboxRelay_RegistersTheRelay_AndItsPublisherAsASingleton()
    {
        var services = NewBuilder().AddOutboxRelay<RecordingPublisher>().Services;

        Assert.True(RelayRegistered(services));
        Assert.Equal(ServiceLifetime.Singleton, Single<RecordingPublisher>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Single<Kaleido.Eventing.IEventPublisher>(services).Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<RecordingPublisher>(), provider.GetRequiredService<Kaleido.Eventing.IEventPublisher>());
    }

    [Fact]
    public void AddOutboxRelay_WithAPublisherTheHostRegistered_UsesThatOne()
    {
        var services = new ServiceCollection();
        var publisher = new RecordingPublisher();
        services.AddSingleton(publisher);

        NewBuilder(services).AddOutboxRelay<RecordingPublisher>();

        using var provider = services.BuildServiceProvider();
        Assert.Same(publisher, provider.GetRequiredService<Kaleido.Eventing.IEventPublisher>());
    }

    [Fact]
    public void AddOutboxRelay_Twice_WithTheSamePublisher_RegistersOneRelay()
    {
        var services = NewBuilder().AddOutboxRelay<RecordingPublisher>().AddOutboxRelay<RecordingPublisher>().Services;

        Assert.Single(services, x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && x.ImplementationType == typeof(Kaleido.Eventing.KaleidoEventRelay));
        Assert.Single(services, x => x.ServiceType == typeof(Kaleido.Eventing.IEventPublisher));
    }

    // A host runs one relay, with one publisher.
    [Fact]
    public void AddOutboxRelay_WithAnotherPublisher_IsAConfigurationError()
    {
        var builder = NewBuilder().AddOutboxRelay<RecordingPublisher>();

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddOutboxRelay<OtherPublisher>);

        Assert.Equal(ConfigurationErrorCodes.ConflictingOutbox, exception.Code);
    }

    [Fact]
    public void AddOutboxRelay_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOutboxRelay<RecordingPublisher>());
    }
    // ---------------------------------------------------------------------------
    // Event classification redaction (ADR 0025)

    private static Kaleido.Eventing.KaleidoEventRedactionMap RedactionMap(IServiceCollection services) =>
        Assert.IsType<Kaleido.Eventing.KaleidoEventRedactionMap>(Single<Kaleido.Eventing.KaleidoEventRedactionMap>(services).ImplementationInstance);

    // Without AddEventRedaction the event policy is empty (classified values are erased, with a warning).
    [Fact]
    public void AddKaleido_RegistersAnEmptyEventRedactionPolicy_AndNoIRedactorProvider()
    {
        var services = NewBuilder().Services;

        Assert.False(RedactionMap(services).IsConfigured);
        Assert.Equal(typeof(Kaleido.Eventing.KaleidoEventRedactorProvider), Single<Kaleido.Eventing.IKaleidoEventRedactorProvider>(services).ImplementationType);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(Microsoft.Extensions.Compliance.Redaction.IRedactorProvider));
    }

    [Fact]
    public void AddEventRedaction_ConfiguresTheEventPolicy_AddingToEachCall()
    {
        var pii = new Microsoft.Extensions.Compliance.Classification.DataClassification("test", "pii");
        var phi = new Microsoft.Extensions.Compliance.Classification.DataClassification("test", "phi");

        var builder = NewBuilder();
        Assert.Same(builder, builder.AddEventRedaction(r => r.SetRedactor<Microsoft.Extensions.Compliance.Redaction.NullRedactor>(new Microsoft.Extensions.Compliance.Classification.DataClassificationSet(pii))));
        builder.AddEventRedaction(r => r.SetFallbackRedactor<Microsoft.Extensions.Compliance.Redaction.NullRedactor>());

        var map = RedactionMap(builder.Services);
        Assert.True(map.IsConfigured);
        Assert.Equal(typeof(Microsoft.Extensions.Compliance.Redaction.NullRedactor), map.RedactorFor(new(pii)));
        Assert.Equal(typeof(Microsoft.Extensions.Compliance.Redaction.NullRedactor), map.RedactorFor(new(phi)));
    }

    // Called on a builder whose collection has no policy yet (a host composing its own builder).
    [Fact]
    public void AddEventRedaction_WithoutAPolicyRegistered_RegistersOne()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Services).Returns(services);

        builder.Object.AddEventRedaction(_ => { });

        Assert.True(RedactionMap(services).IsConfigured);
    }

    [Fact]
    public void AddEventRedaction_WithNullArguments_Throws()
    {
        IKaleidoBuilder? none = null;

        Assert.Throws<ArgumentNullException>(() => none!.AddEventRedaction(_ => { }));
        Assert.Throws<ArgumentNullException>(() => NewBuilder().AddEventRedaction(null!));
    }

    // ---------------------------------------------------------------------------
    // Stores, transactions and the outbox choice (ADR 0023)

    private static IKaleidoBuilder NewBuilder(IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddKaleido(EmptyConfig(), DefaultServiceOptions());

    private static Type? Registered<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService))?.ImplementationType;

    private static bool RelayRegistered(IServiceCollection services) =>
        services.Any(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && x.ImplementationType == typeof(Kaleido.Eventing.KaleidoEventRelay));

    [Fact]
    public void AddKaleido_ByDefault_UsesKaleidosOutboxInMemory_AndNoTransactions()
    {
        var services = NewBuilder().Services;

        Assert.Equal(typeof(Kaleido.Eventing.KaleidoStoreOutbox), Registered<Kaleido.Storage.IKaleidoOutbox>(services));
        Assert.Equal(typeof(Kaleido.Storage.InMemoryKaleidoOutboxStore), Registered<Kaleido.Storage.IKaleidoOutboxStore>(services));
        Assert.Equal(typeof(Kaleido.Storage.NoKaleidoTransactions), Registered<Kaleido.Storage.IKaleidoTransactions>(services));
    }
    [Fact]
    public void AddContextStore_IdempotencyStore_AndTransactions_ReplaceTheDefaults_AsScoped()
    {
        var services = NewBuilder().AddContextStore<OwnContextStore>().AddIdempotencyStore<OwnIdempotencyStore>().AddTransactions<OwnTransactions>().Services;

        Assert.Equal(ServiceLifetime.Scoped, Single<Kaleido.Storage.IKaleidoContextStore>(services).Lifetime);
        Assert.Equal(typeof(OwnContextStore), Registered<Kaleido.Storage.IKaleidoContextStore>(services));
        Assert.Equal(typeof(OwnIdempotencyStore), Single<Kaleido.Storage.IKaleidoIdempotencyStore>(services).ImplementationType);
        Assert.Equal(typeof(OwnTransactions), Single<Kaleido.Storage.IKaleidoTransactions>(services).ImplementationType);
    }

    [Fact]
    public void AddContextStore_TheSameStoreTwice_IsRegisteredOnce()
    {
        var services = NewBuilder().AddContextStore<OwnContextStore>().AddContextStore<OwnContextStore>().Services;

        Assert.Equal(typeof(OwnContextStore), Single<Kaleido.Storage.IKaleidoContextStore>(services).ImplementationType);
    }

    // One store per service: a second, different one (by type, instance or factory) is a mistake.
    [Fact]
    public void AddContextStore_WhenAnotherStoreIsRegistered_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        var builder = NewBuilder(services);
        services.AddScoped(_ => Mock.Of<Kaleido.Storage.IKaleidoContextStore>());

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddContextStore<OwnContextStore>);

        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, exception.Code);
    }

    [Fact]
    public void AddOutboxStore_StoresTheOutbox()
    {
        var services = NewBuilder().AddOutboxStore<OwnOutboxStore>().Services;

        Assert.Equal(typeof(OwnOutboxStore), Single<Kaleido.Storage.IKaleidoOutboxStore>(services).ImplementationType);
        Assert.Equal(typeof(Kaleido.Eventing.KaleidoStoreOutbox), Registered<Kaleido.Storage.IKaleidoOutbox>(services));
    }

    // One outbox store per service.
    [Fact]
    public void TwoOutboxStores_AreAConfigurationError()
    {
        var builder = NewBuilder().AddOutboxStore<OwnOutboxStore>();

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddOutboxStore<OtherOutboxStore>);

        Assert.Equal(ConfigurationErrorCodes.ConflictingOutbox, exception.Code);
    }

    [Fact]
    public void TheStoreMethods_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddContextStore<OwnContextStore>());
        Assert.Throws<ArgumentNullException>(() => builder!.AddOutboxStore<OwnOutboxStore>());
    }
    // ---------------------------------------------------------------------------
    // Test doubles

    private sealed class OwnContextStore : Kaleido.Storage.IKaleidoContextStore
    {
        public Task<Kaleido.Processor.Context.ProcessorContext?> LoadAsync(Kaleido.Storage.ProcessKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Kaleido.Processor.Context.ProcessorContext> SaveAsync(Kaleido.Processor.Context.ProcessorContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OwnIdempotencyStore : Kaleido.Storage.IKaleidoIdempotencyStore
    {
        public Task<Kaleido.Storage.IdempotentResult?> AdmitAsync(Kaleido.Storage.IdempotentResult admission, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> CompleteAsync(Kaleido.Storage.IdempotentResult result, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OwnTransactions : Kaleido.Storage.IKaleidoTransactions
    {
        public Task<Kaleido.Storage.IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private class OwnOutboxStore : Kaleido.Storage.IKaleidoOutboxStore
    {
        public Task AddAsync(IReadOnlyCollection<Kaleido.Storage.KaleidoOutboxEvent> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Kaleido.Storage.KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordRelayAsync(Kaleido.Storage.KaleidoOutboxRelay relay, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Kaleido.Storage.KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OtherOutboxStore : OwnOutboxStore;

    private class RecordingPublisher : Kaleido.Eventing.IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : Kaleido.Eventing.IKaleidoEvent => Task.CompletedTask;
    }

    private sealed class OtherPublisher : RecordingPublisher;
    // ---------------------------------------------------------------------------

    private sealed class CustomCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }

    private sealed class CustomCorrelationContextInitializer : IKaleidoCorrelationContextInitializer
    {
        public void Initialize(KaleidoCorrelationContext context) { }
    }

    private sealed class FirstRedactor : Kaleido.Eventing.IKaleidoEventRedactor
    {
        public Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> envelope)
            where TEvent : Kaleido.Eventing.IKaleidoEvent => envelope;
    }

    private sealed class SecondRedactor : Kaleido.Eventing.IKaleidoEventRedactor
    {
        public Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> envelope)
            where TEvent : Kaleido.Eventing.IKaleidoEvent => envelope;
    }

    // #552: every service watches its outbox.
    [Fact]
    public void AddKaleido_RegistersTheOutboxHealthCheck_Once()
    {
        var services = new ServiceCollection();
        NewBuilder(services);
        services.AddHealthChecks();

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>().Value.Registrations;

        var outbox = Assert.Single(registrations, x => x.Name == "kaleido-outbox");
        Assert.Contains("outbox", outbox.Tags);
    }
}
