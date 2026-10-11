using Kaleido.Exceptions;
using Kaleido.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static Mock<IKaleidoBuilder> Builder(ServiceCollection services)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().Build());
        return builder;
    }

    [Fact]
    public void AddMcp_WithoutAddHttp_FailsWithItsCode()
    {
        var builder = Builder([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp());

        Assert.Equal(McpErrorCodes.MissingHttp, exception.Code);
    }

    [Fact]
    public void AddMcp_WithoutKaleidoClients_FailsWithItsCode()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoRegistryService>());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(services).Object.AddMcp());

        Assert.Equal(McpErrorCodes.MissingClients, exception.Code);
    }

    [Fact]
    public void AddMcp_OnAKaleidoHttpHostWithClients_RegistersTheAdapter_AndAppliesTheConfiguration()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoRegistryService>());
        services.AddSingleton(Mock.Of<IKaleidoCallerForwarding>());

        Builder(services).Object.AddMcp(o => o.ServerName = "priorauth");

        var options = Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpOptions)).ImplementationInstance;
        Assert.Equal("priorauth", Assert.IsType<KaleidoMcpOptions>(options).ServerName);
    }
}
