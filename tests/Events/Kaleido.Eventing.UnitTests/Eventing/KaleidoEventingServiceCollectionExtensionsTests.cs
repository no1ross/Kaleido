using Kaleido.Exceptions;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventingServiceCollectionExtensionsTests : SutFixture
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static KaleidoServiceOptions DefaultServiceOptions() => new()
    {
        ServiceName = "test-service",
        ServiceVersion = "1",
        Assemblies = [typeof(KaleidoEventingServiceCollectionExtensionsTests).Assembly],
    };

    private static IKaleidoBuilder NewBuilder(IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddKaleido(EmptyConfig(), DefaultServiceOptions()).AddEventing();

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    private static Type? Registered<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService))?.ImplementationType;

    private static bool RelayRegistered(IServiceCollection services) =>
        services.Any(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && x.ImplementationType == typeof(KaleidoEventRelay));

    private static KaleidoEventRedactionMap RedactionMap(IServiceCollection services) =>
        Assert.IsType<KaleidoEventRedactionMap>(Single<KaleidoEventRedactionMap>(services).ImplementationInstance);

    // ADR 0030: configuration overrides code only for the settings it names, without changing code options.
    [Fact]
    public void AddEventing_LaysConfigurationOverCode_WithoutChangingIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Events:BatchSize"] = "7" })
            .Build();
        var code = new KaleidoEventOptions { BatchSize = 3, MaxConcurrentPublishes = 2 };
        var services = new ServiceCollection();

        services.AddKaleido(config, DefaultServiceOptions()).AddEventing(code);

        var registered = Assert.IsType<KaleidoEventOptions>(Single<KaleidoEventOptions>(services).ImplementationInstance);
        Assert.Equal(7, registered.BatchSize);
        Assert.Equal(2, registered.MaxConcurrentPublishes);
        Assert.Equal(3, code.BatchSize);
    }

    [Fact]
    public void AddEventing_AConfiguredValueThatCannotBeRead_IsAConfigurationError()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Events:BatchSize"] = "many" })
            .Build();
        var builder = new ServiceCollection().AddKaleido(config, DefaultServiceOptions());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddEventing());

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains("Kaleido:Events:BatchSize", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEventing_InvalidSemanticValue_ThrowsInvalidEventOptions()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            new ServiceCollection().AddKaleido(EmptyConfig(), DefaultServiceOptions()).AddEventing(new() { BatchSize = 0 }));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
    }

    [Fact]
    public void AddEventing_CalledTwice_IsAConfigurationError()
    {
        var builder = NewBuilder();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddEventing());

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    [Fact]
    public void AddEventing_WithoutABuilder_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddEventing());
    }

    // The foundation does not override an outbox chosen by the host before Eventing registration.
    [Fact]
    public void AddEventing_RegistersTheOutboxStore_AndAHostsOwnStoreWins()
    {
        var services = new ServiceCollection();
        var ownOutbox = Mock.Of<IKaleidoOutboxStore>();
        services.AddSingleton(ownOutbox);

        NewBuilder(services);

        Assert.Same(ownOutbox, Single<IKaleidoOutboxStore>(services).ImplementationInstance);
    }

    [Fact]
    public void AddEventRedactor_RegistersEachRedactorInOrder()
    {
        var services = new ServiceCollection();
        NewBuilder(services)
            .AddEventRedactor<FirstRedactor>()
            .AddEventRedactor<SecondRedactor>();

        using var provider = services.BuildServiceProvider(validateScopes: false);

        Assert.Equal([typeof(FirstRedactor), typeof(SecondRedactor)], provider.GetServices<IKaleidoEventRedactor>().Select(x => x.GetType()));
    }

    [Fact]
    public void AddEventRedactor_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddEventRedactor<FirstRedactor>());
    }

    // #552: no publisher and no relay by default - every service only writes its events.
    [Fact]
    public void AddEventing_RegistersNoPublisherAndNoRelay()
    {
        var services = NewBuilder().Services;

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IEventPublisher));
        Assert.False(RelayRegistered(services));
    }

    // The relay owns the publisher: registered as a singleton, unless the host registered it itself.
    [Fact]
    public void AddOutboxRelay_RegistersTheRelay_AndItsPublisherAsASingleton()
    {
        var services = NewBuilder().AddOutboxRelay<RecordingPublisher>().Services;

        Assert.True(RelayRegistered(services));
        Assert.Equal(ServiceLifetime.Singleton, Single<RecordingPublisher>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Single<IEventPublisher>(services).Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<RecordingPublisher>(), provider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public void AddOutboxRelay_WithAPublisherTheHostRegistered_UsesThatOne()
    {
        var services = new ServiceCollection();
        var publisher = new RecordingPublisher();
        services.AddSingleton(publisher);

        NewBuilder(services).AddOutboxRelay<RecordingPublisher>();

        using var provider = services.BuildServiceProvider();
        Assert.Same(publisher, provider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public void AddOutboxRelay_Twice_WithTheSamePublisher_RegistersOneRelay()
    {
        var services = NewBuilder().AddOutboxRelay<RecordingPublisher>().AddOutboxRelay<RecordingPublisher>().Services;

        Assert.Single(services, x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && x.ImplementationType == typeof(KaleidoEventRelay));
        Assert.Single(services, x => x.ServiceType == typeof(IEventPublisher));
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
    public void AddOutboxRelay_WithoutEventing_IsAConfigurationError()
    {
        var builder = new ServiceCollection().AddKaleido(EmptyConfig(), DefaultServiceOptions());

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddOutboxRelay<RecordingPublisher>);

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
    }

    [Fact]
    public void AddOutboxRelay_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOutboxRelay<RecordingPublisher>());
    }

    // Event classification redaction (ADR 0025).
    // Without AddEventRedaction the event policy is empty (classified values are erased, with a warning).
    [Fact]
    public void AddEventing_RegistersAnEmptyEventRedactionPolicy_AndNoIRedactorProvider()
    {
        var services = NewBuilder().Services;

        Assert.False(RedactionMap(services).IsConfigured);
        Assert.Equal(typeof(KaleidoEventRedactorProvider), Single<IKaleidoEventRedactorProvider>(services).ImplementationType);
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

    [Fact]
    public void AddEventing_DoesNotSelectAnOutboxStore()
    {
        var services = NewBuilder().Services;

        Assert.Equal(typeof(KaleidoStoreOutbox), Registered<IKaleidoOutbox>(services));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IKaleidoOutboxStore));
    }

    [Fact]
    public void AddOutboxStore_StoresTheOutbox()
    {
        var services = NewBuilder().AddOutboxStore<OwnOutboxStore>().Services;

        Assert.Equal(typeof(OwnOutboxStore), Single<IKaleidoOutboxStore>(services).ImplementationType);
        Assert.Equal(typeof(KaleidoStoreOutbox), Registered<IKaleidoOutbox>(services));
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
    public void AddOutboxStore_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOutboxStore<OwnOutboxStore>());
    }

    // #552: every service with Eventing watches its outbox.
    [Fact]
    public void AddEventing_RegistersTheOutboxHealthCheck_Once()
    {
        var services = new ServiceCollection();
        NewBuilder(services);
        services.AddHealthChecks();

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>().Value.Registrations;

        var outbox = Assert.Single(registrations, x => x.Name == "kaleido-outbox");
        Assert.Contains("outbox", outbox.Tags);
    }

    private class OwnOutboxStore : IKaleidoOutboxStore
    {
        public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RecordRelayAsync(KaleidoOutboxRelay relay, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OtherOutboxStore : OwnOutboxStore;

    private class RecordingPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent => Task.CompletedTask;
    }

    private sealed class OtherPublisher : RecordingPublisher;

    private sealed class FirstRedactor : IKaleidoEventRedactor
    {
        public KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
            where TEvent : IKaleidoEvent => envelope;
    }

    private sealed class SecondRedactor : IKaleidoEventRedactor
    {
        public KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
            where TEvent : IKaleidoEvent => envelope;
    }
}
