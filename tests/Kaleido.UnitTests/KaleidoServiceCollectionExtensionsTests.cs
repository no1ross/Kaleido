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
    private static Action<KaleidoServiceOptionsBuilder> DefaultServiceOptions() =>
        o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = [typeof(KaleidoServiceCollectionExtensionsTests).Assembly];
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

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly };
        });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Same(assembly, Assert.Single(concreteBuilder.Assemblies));
    }

    [Fact]
    public void AddKaleido_WithMultipleAssembliesInOptions_RegistersAllAssemblies()
    {
        var services = new ServiceCollection();
        var assembly1 = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        var assembly2 = typeof(IServiceCollection).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly1, assembly2 };
        });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly1, concreteBuilder.Assemblies);
        Assert.Contains(assembly2, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithDuplicateAssembliesInOptions_DeduplicatesAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly, assembly };
        });

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
            services.AddKaleido(EmptyConfig(), o =>
            {
                o.ServiceName = "test-service";
                if (empty)
                {
                    o.Assemblies = [];
                }
            }));

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
            services.AddKaleido(EmptyConfig(), o =>
            {
                o.ServiceName = "test-service";
                o.DefaultAuthorization = new AuthorizationMetadata("staff", []) { AllowAnonymous = true };
            }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void AddKaleido_RejectsUnknownAuthorizationMode()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), o =>
            {
                o.ServiceName = "test-service";
                o.AuthorizationMode = (KaleidoAuthorizationMode)123;
            }));

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

    [Fact]
    public void AddEventPublisher_ReplacesTheDefaultPublisherWithASingleton()
    {
        var services = new ServiceCollection();

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions())
            .AddEventPublisher<RecordingPublisher>();

        var registration = Assert.Single(services, x => x.ServiceType == typeof(Kaleido.Eventing.IEventPublisher));
        Assert.Equal(typeof(RecordingPublisher), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void AddEventPublisher_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddEventPublisher<RecordingPublisher>());
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
    public void AddKaleido_ByDefault_UsesKaleidosOutboxInMemory_WithItsRelay_AndNoTransactions()
    {
        var services = NewBuilder().Services;

        Assert.Equal(typeof(Kaleido.Eventing.KaleidoStoreOutbox), Registered<Kaleido.Storage.IKaleidoOutbox>(services));
        Assert.Equal(typeof(Kaleido.Storage.InMemoryKaleidoOutboxStore), Registered<Kaleido.Storage.IKaleidoOutboxStore>(services));
        Assert.Equal(typeof(Kaleido.Storage.NoKaleidoTransactions), Registered<Kaleido.Storage.IKaleidoTransactions>(services));
        Assert.True(RelayRegistered(services));
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
    public void AddOutboxStore_StoresKaleidosOutbox_AndKeepsTheRelay()
    {
        var services = NewBuilder().AddOutboxStore<OwnOutboxStore>().Services;

        Assert.Equal(typeof(OwnOutboxStore), Single<Kaleido.Storage.IKaleidoOutboxStore>(services).ImplementationType);
        Assert.Equal(typeof(Kaleido.Eventing.KaleidoStoreOutbox), Registered<Kaleido.Storage.IKaleidoOutbox>(services));
        Assert.True(RelayRegistered(services));
    }

    [Fact]
    public void AddOutbox_ReplacesKaleidosOutbox_AndRemovesItsStoreAndRelay()
    {
        var services = NewBuilder().AddOutbox<OwnOutbox>().Services;

        Assert.Equal(typeof(OwnOutbox), Single<Kaleido.Storage.IKaleidoOutbox>(services).ImplementationType);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(Kaleido.Storage.IKaleidoOutboxStore));
        Assert.False(RelayRegistered(services));
    }

    [Fact]
    public void AddDirectPublication_ReplacesKaleidosOutbox_AndRemovesItsStoreAndRelay()
    {
        var services = NewBuilder().AddDirectPublication().Services;

        Assert.Equal(typeof(Kaleido.Eventing.KaleidoDirectPublicationOutbox), Single<Kaleido.Storage.IKaleidoOutbox>(services).ImplementationType);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(Kaleido.Storage.IKaleidoOutboxStore));
        Assert.False(RelayRegistered(services));
    }

    // One outbox choice per service.
    public static TheoryData<string> ConflictingOutboxChoices() =>
        ["store then own", "own then store", "own then direct", "direct then own", "two stores"];

    [Theory]
    [MemberData(nameof(ConflictingOutboxChoices))]
    public void TwoOutboxChoices_AreAConfigurationError(string choices)
    {
        var builder = NewBuilder();
        Action second = choices switch
        {
            "store then own" => () => builder.AddOutboxStore<OwnOutboxStore>().AddOutbox<OwnOutbox>(),
            "own then store" => () => builder.AddOutbox<OwnOutbox>().AddOutboxStore<OwnOutboxStore>(),
            "own then direct" => () => builder.AddOutbox<OwnOutbox>().AddDirectPublication(),
            "direct then own" => () => builder.AddDirectPublication().AddOutbox<OwnOutbox>(),
            _ => () => builder.AddOutboxStore<OwnOutboxStore>().AddOutboxStore<OtherOutboxStore>(),
        };

        Assert.Equal(ConfigurationErrorCodes.ConflictingOutbox, Assert.Throws<KaleidoConfigurationException>(second).Code);
    }

    [Fact]
    public void TheStoreAndOutboxMethods_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddContextStore<OwnContextStore>());
        Assert.Throws<ArgumentNullException>(() => builder!.AddOutboxStore<OwnOutboxStore>());
        Assert.Throws<ArgumentNullException>(() => builder!.AddOutbox<OwnOutbox>());
        Assert.Throws<ArgumentNullException>(() => builder!.AddDirectPublication());
    }

    // ---------------------------------------------------------------------------
    // Test doubles

    private sealed class OwnContextStore : Kaleido.Storage.IKaleidoContextStore
    {
        public Task<Kaleido.Processor.Context.ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Kaleido.Processor.Context.ProcessorContext> SaveAsync(Kaleido.Processor.Context.ProcessorContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OwnIdempotencyStore : Kaleido.Storage.IKaleidoIdempotencyStore
    {
        public Task<Kaleido.Processor.Context.IdempotentResult?> AdmitAsync(Kaleido.Processor.Context.IdempotentResult admission, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CompleteAsync(Kaleido.Processor.Context.IdempotentResult result, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReleaseAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
    }

    private sealed class OtherOutboxStore : OwnOutboxStore;

    private sealed class OwnOutbox : Kaleido.Storage.IKaleidoOutbox
    {
        public Task AddAsync(IReadOnlyCollection<Kaleido.Storage.KaleidoOutboxEvent> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPublisher : Kaleido.Eventing.IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(Kaleido.Eventing.KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : Kaleido.Eventing.IKaleidoEvent => Task.CompletedTask;
    }
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
}
