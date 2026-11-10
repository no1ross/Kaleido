using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Storage.UnitTests;

// ADR 0028 (#438): a store the service uses is never left in memory by accident.
public sealed class KaleidoStoreSelectionCheckTests
    : SutFixture
{
    private static KaleidoStoreSelectionCheck CreateSut(IServiceCollection services) => new(services);

    // What AddKaleido registers: the outbox default; a capability contributes its own store choices.
    private static ServiceCollection Defaults(params string[] capabilityStores)
    {
        var services = new ServiceCollection();
        services.AddScoped<IKaleidoOutbox, KaleidoStoreOutbox>();
        services.AddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();

        if (capabilityStores.Length > 0)
        {
            services.AddSingleton(new KaleidoStoreSelectionContributor(_ => capabilityStores));
        }

        return services;
    }

    private static Task StartAsync(IServiceCollection services) =>
        CreateSut(services).StartAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task AServiceWithProcesses_LeftAtTheDefaults_DoesNotStart_NamingTheStores()
    {
        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(Defaults("process state (IKaleidoContextStore)", "idempotency records (IKaleidoIdempotencyStore)")));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("process state (IKaleidoContextStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("idempotency records (IKaleidoIdempotencyStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("event outbox (IKaleidoOutboxStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddInMemoryStores()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExplicitInMemoryChoice_Starts()
    {
        var services = Defaults("process state (IKaleidoContextStore)", "idempotency records (IKaleidoIdempotencyStore)");
        services.AddSingleton<KaleidoInMemoryStoresChoice>();

        await StartAsync(services);
        await CreateSut(services).StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DurableStores_Start()
    {
        var services = Defaults();
        services.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        await StartAsync(services);
    }

    [Fact]
    public async Task OneStoreLeftInMemory_IsTheOneNamed()
    {
        var services = Defaults("process state (IKaleidoContextStore)");
        services.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Contains("process state", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotency records", exception.Message, StringComparison.Ordinal);
    }

    // A query-only service keeps neither process state nor idempotency records, but it writes its events
    // (#552): its outbox store must be chosen too.
    [Fact]
    public void AQueryOnlyService_UsesOnlyTheOutboxStore()
    {
        Assert.Equal(["event outbox (IKaleidoOutboxStore)"], KaleidoStoreSelectionCheck.InMemoryStoresInUse(Defaults()));
    }

    // Whether or not this host publishes: every service writes its events (#552).
    [Fact]
    public void ADurableOutboxStore_NeedsNoChoice()
    {
        var durable = Defaults();
        durable.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(durable));
    }
}
