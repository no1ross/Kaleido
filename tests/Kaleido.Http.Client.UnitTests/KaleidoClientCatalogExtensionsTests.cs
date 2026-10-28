using Kaleido.Exceptions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientCatalogExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoClientDefinition Client(string name) =>
        new(name, name.ToLowerInvariant(), RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    [Fact]
    public void Require_ReturnsTheConfiguredClient()
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        var radiology = Client("Radiology");
        catalog.Setup(x => x.Find("radiology")).Returns(radiology);

        Assert.Same(radiology, catalog.Object.Require("radiology"));
    }

    [Fact]
    public void Require_ForAnUnconfiguredName_FailsNamingTheConfiguredClientsInOrder()
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Clients).Returns([Client("Radiology"), Client("History")]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => catalog.Object.Require("Radiolgy"));

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
        Assert.Equal("Kaleido client 'Radiolgy' is not configured under Kaleido:Clients; configured clients: History, Radiology.", exception.Message);
    }

    [Fact]
    public void Require_WhenNoClientIsConfigured_SaysSo()
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Clients).Returns([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => catalog.Object.Require("Member"));

        Assert.Equal("Kaleido client 'Member' is not configured under Kaleido:Clients; no clients are configured.", exception.Message);
    }
}
