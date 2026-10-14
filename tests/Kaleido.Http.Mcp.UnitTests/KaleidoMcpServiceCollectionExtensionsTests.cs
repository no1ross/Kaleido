using Kaleido.Exceptions;
using Kaleido.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static Mock<IKaleidoBuilder> Builder(ServiceCollection services, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build());
        return builder;
    }

    private static ServiceCollection HostServices()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoRegistryService>());
        services.AddSingleton(Mock.Of<IKaleidoCallerForwarding>());
        return services;
    }

    private static KaleidoMcpOptions RegisteredOptions(ServiceCollection services) =>
        Assert.IsType<KaleidoMcpOptions>(Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpOptions)).ImplementationInstance);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("3", 3)]
    public void AddMcp_BindsMaxElicitationRounds_FromConfiguration(string configured, int expected)
    {
        var services = HostServices();

        Builder(services, new Dictionary<string, string?> { ["Kaleido:Mcp:MaxElicitationRounds"] = configured }).Object.AddMcp();

        Assert.Equal(expected, RegisteredOptions(services).MaxElicitationRounds);
    }

    [Fact]
    public void AddMcp_NegativeMaxElicitationRoundsInConfiguration_FailsWithItsCode()
    {
        var builder = Builder(HostServices(), new Dictionary<string, string?> { ["Kaleido:Mcp:MaxElicitationRounds"] = "-1" });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp());

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
    }

    [Fact]
    public void AddMcp_NegativeMaxElicitationRoundsInConfigure_FailsWithItsCode()
    {
        var builder = Builder(HostServices());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp(o => o.MaxElicitationRounds = -1));

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
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
