using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Storage.UnitTests;

// ADR 0028 (#438): a store the service uses is never left in memory by accident.
public sealed class KaleidoStoreSelectionCheckTests
    : SutFixture
{
    private static KaleidoStoreSelectionCheck CreateSut(IServiceCollection services) => new(services);

    // Capabilities contribute only the stores they use; the check aggregates their choices.
    private static ServiceCollection Defaults(params string[] stores)
    {
        var services = new ServiceCollection();
        if (stores.Length > 0)
        {
            services.AddSingleton(new KaleidoStoreSelectionContributor(_ => stores));
        }

        return services;
    }

    private static Task StartAsync(IServiceCollection services) =>
        CreateSut(services).StartAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task SeveralContributors_LeftAtTheDefaults_DoNotStart_NamingAllStores()
    {
        var services = Defaults("process state (IKaleidoContextStore)");
        services.AddSingleton(new KaleidoStoreSelectionContributor(_ => ["idempotency records (IKaleidoIdempotencyStore)"]));

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("process state (IKaleidoContextStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("idempotency records (IKaleidoIdempotencyStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddInMemoryStores()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExplicitInMemoryChoice_Starts()
    {
        var services = Defaults("process state (IKaleidoContextStore)", "idempotency records (IKaleidoIdempotencyStore)");
        services.AddSingleton(typeof(KaleidoInMemoryStoresChoice), KaleidoInMemoryStoresChoice.Explicit);

        await StartAsync(services);
        await CreateSut(services).StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DurableStores_Start()
    {
        await StartAsync(Defaults());
    }

    [Fact]
    public async Task OneStoreLeftInMemory_IsTheOneNamed()
    {
        var services = Defaults("process state (IKaleidoContextStore)");

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Contains("process state", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotency records", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AServiceWithoutStoreContributors_NeedsNoChoice()
    {
        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(Defaults()));
    }
}
