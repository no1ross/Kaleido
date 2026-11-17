using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Registry.Client.UnitTests;

// #553: ready once registered and the registry read once; never not ready again for a later failure.
public sealed class RegistrationHealthCheckTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IControlRegistryRegistrar> registrar = new();
    private readonly Mock<ICentralRegistrySource> registry = new();

    private RegistrationHealthCheck CreateSut() => new(registrar.Object, registry.Object);

    [Theory]
    [InlineData(false, false, HealthStatus.Unhealthy, "Not registered")]
    [InlineData(false, true, HealthStatus.Unhealthy, "Not registered")]
    [InlineData(true, false, HealthStatus.Unhealthy, "not been read")]
    [InlineData(true, true, HealthStatus.Healthy, "Registered with")]
    public async Task Ready_OnceRegistered_AndTheRegistryRead(bool registered, bool read, HealthStatus expected, string reason)
    {
        registrar.Setup(x => x.HasRegistered).Returns(registered);
        registrar.Setup(x => x.Publishes).Returns(true);
        registry.Setup(x => x.HasRead).Returns(read);

        var result = await CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
        Assert.Contains(reason, result.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, HealthStatus.Unhealthy)]
    [InlineData(true, HealthStatus.Healthy)]
    public async Task ConsumerOnlyHost_IsReadyAfterItsFirstRegistryRead(bool read, HealthStatus expected)
    {
        registrar.Setup(x => x.Publishes).Returns(false);
        registry.Setup(x => x.HasRead).Returns(read);

        var result = await CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
    }
}
