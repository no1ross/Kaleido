using Kaleido.Exceptions;
using Kaleido.Http.Processor;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoClientCatalog> catalog = new();
    private readonly IKaleidoProcessorClient intake = Mock.Of<IKaleidoProcessorClient>();

    private KaleidoProcessorClientFactory CreateSut()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("IntakeService", intake);

        return new(services.BuildServiceProvider(), catalog.Object);
    }

    private static KaleidoClientDefinition Intake =>
        new("IntakeService", "intake", RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    [Fact]
    public void GetClient_ForAnUnconfiguredName_FailsWithUnknownClient()
    {
        catalog.Setup(x => x.Clients).Returns([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut().GetClient("Radiolgy"));

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
    }

    // The catalog matches the name case-insensitively; the keyed client is resolved under the
    // configured name.
    [Fact]
    public void GetClient_ResolvesTheClientRegisteredUnderTheConfiguredName()
    {
        catalog.Setup(x => x.Find("intakeservice")).Returns(Intake);

        Assert.Same(intake, CreateSut().GetClient("intakeservice"));
    }

    [Fact]
    public void TryGetClientForService_FindsTheClientByItsRoutePrefix()
    {
        catalog.Setup(x => x.FindByService("Intake")).Returns(Intake);

        Assert.True(CreateSut().TryGetClientForService("Intake", out var client));
        Assert.Same(intake, client);
    }

    [Fact]
    public void TryGetClientForService_ForAServiceWithoutAClient_ReturnsFalse()
    {
        Assert.False(CreateSut().TryGetClientForService("radiology", out var client));
        Assert.Null(client);
    }
}
