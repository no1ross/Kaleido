using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Registry.UnitTests;

public sealed class RegistryStoreHealthCheckTests
    : SutFixture
{
    private readonly Mock<IKaleidoRegistryStore> store = new();

    private RegistryStoreHealthCheck CreateSut() => new(store.Object);

    [Fact]
    public async Task AStoreThatAnswers_IsHealthy()
    {
        store.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RegistryStoreContents());

        Assert.Equal(HealthStatus.Healthy, (await CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)).Status);
    }

    // The health check service turns the failure into the registration's failure status (Unhealthy).
    [Fact]
    public async Task AStoreThatFails_Throws()
    {
        store.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken));
    }
}
