using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Registration.UnitTests;

public sealed class KaleidoHttpClientPipelineServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    private sealed class HostCredentialHandler : DelegatingHandler;

    private static IKaleidoBuilder Builder(IServiceCollection services, string? gateway = "https://gateway.test/", bool obsoleteClients = false)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        var settings = new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = gateway };
        if (obsoleteClients)
        {
            settings["Kaleido:Clients:clinic:BaseUrl"] = "https://clinic.test/";
        }

        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return builder.Object;
    }

    [Fact]
    public void AddPipelineAndNamedClient_UseTheOneGatewayAndSharedHandlerPolicy()
    {
        var services = new ServiceCollection();
        var builder = Builder(services);
        builder.AddKaleidoClientHandler<HostCredentialHandler>();
        builder.AddKaleidoHttpClientPipeline().AddKaleidoHttpClientPipeline();
        builder.AddKaleidoNamedClient("kaleido-registry");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("kaleido-registry");
        Assert.Equal(new Uri("https://gateway.test/"), client.BaseAddress);
        Assert.Single(services, x => x.ImplementationInstance is KaleidoClientHandlerRegistration);
        Assert.Single(services, x => x.ImplementationInstance is KaleidoHttpClientName);
        Assert.Single(services, x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && x.ImplementationType == typeof(KaleidoClientTransportCheck));
    }

    [Fact]
    public void AddPipeline_RejectsObsoletePerServiceClientAddresses()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), obsoleteClients: true).AddKaleidoHttpClientPipeline());

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:Clients", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-gateway")]
    [InlineData("https://name:password@gateway.test/")]
    public void AddPipeline_RejectsAnInvalidGateway(string? gateway)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), gateway).AddKaleidoHttpClientPipeline());
        Assert.Contains("BaseUrl", exception.Message, StringComparison.Ordinal);
    }
}
