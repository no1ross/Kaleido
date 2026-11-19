using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Hosting.UnitTests;

public sealed class KaleidoProviderRegistrationCheckTests : SutFixture
{
    private static KaleidoProviderRegistrationCheck CreateSut(IServiceCollection services) => new(services);

    private static Task StartAsync(IServiceCollection services) =>
        CreateSut(services).StartAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task StartAsync_WithNoCapabilitiesOrEventing_NeedsNoStore()
    {
        var services = new ServiceCollection();

        await StartAsync(services);
        await CreateSut(services).StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WithProcessSteps_RequiresOnlyContextState()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new KaleidoProcessOptions());

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("process state (IProcessContextStore)", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotency", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_WithEventing_RequiresAnOutbox()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new KaleidoEventOptions());

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("event outbox (IKaleidoOutboxStore)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithBothCapabilities_ReportsEachMissingStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new KaleidoProcessOptions());
        services.AddSingleton(new KaleidoEventOptions());

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Contains("process state (IProcessContextStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("event outbox (IKaleidoOutboxStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kaleido.Provider.InMemory", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kaleido.Provider.SQLite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithExplicitStores_AcceptsAnyImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new KaleidoProcessOptions());
        services.AddSingleton(new KaleidoEventOptions());
        services.AddSingleton(Mock.Of<IProcessContextStore>());
        services.AddSingleton(Mock.Of<IKaleidoOutboxStore>());

        await StartAsync(services);
    }
}
