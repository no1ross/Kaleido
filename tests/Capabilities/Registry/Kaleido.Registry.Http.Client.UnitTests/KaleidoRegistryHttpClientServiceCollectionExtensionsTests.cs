using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Registry.Http.Client.UnitTests;

public sealed class KaleidoRegistryHttpClientServiceCollectionExtensionsTests : SutFixture
{
    private static readonly Dictionary<string, string?> Gateway = new() { ["Kaleido:BaseUrl"] = "https://gateway.internal" };

    private static IKaleidoBuilder Builder(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IKaleidoInboundCallerAccessor>(_ => Mock.Of<IKaleidoInboundCallerAccessor>());
        services.AddScoped<IKaleidoCorrelationContextAccessor>(_ => Mock.Of<IKaleidoCorrelationContextAccessor>());
        var identity = new KaleidoServiceOptions { ServiceName = "code-sets", ServiceVersion = "3" };
        services.AddSingleton(identity);
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        builder.Setup(x => x.ServiceOptions).Returns(identity);
        return builder.Object;
    }

    [Fact]
    public void AddRegistryHttpParticipation_RegistersTheNamedClientAndHydrationLifecycle()
    {
        var builder = Builder(Gateway);
        builder.AddRegistryHttpParticipation();

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service.GetType().Name == "CentralRegistrySync");
        Assert.Contains(provider.GetServices<IHostedService>(), service => service.GetType().Name == "ControlRegistryRegistrar");
        Assert.Contains("ready", Assert.Single(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations).Tags);
        Assert.NotEmpty(provider.GetServices<IHttpMessageHandlerBuilderFilter>());
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(KaleidoHttpRegistryClient.ClientName);
        Assert.Equal(new Uri("https://gateway.internal/"), client.BaseAddress);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PipelineBeforeOrAfterRegistryParticipation_UsesOneHydratedLifecycle(bool pipelineFirst)
    {
        var builder = Builder(new Dictionary<string, string?>(Gateway));
        if (pipelineFirst)
        {
            builder.AddKaleidoHttpClientPipeline().AddRegistryHttpParticipation();
        }
        else
        {
            builder.AddRegistryHttpParticipation().AddKaleidoHttpClientPipeline();
        }

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Single(provider.GetServices<IHostedService>(), service => service.GetType().Name == "CentralRegistrySync");
        Assert.Single(provider.GetServices<IHostedService>(), service => service.GetType().Name == "ControlRegistryRegistrar");
    }

    [Fact]
    public void AddRegistryHttpClient_RegistersNoHostedParticipant()
    {
        var builder = Builder(Gateway);
        builder.AddRegistryHttpClient();

        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(IHostedService)
            && x.ImplementationType?.Name is "CentralRegistrySync" or "ControlRegistryRegistrar");
    }

    [Fact]
    public void AddRegistryHttpParticipation_RequiresTheGatewayBaseUrl()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder([]).AddRegistryHttpParticipation());
        Assert.Equal(HttpClientErrorCodes.MissingBaseUrl, exception.Code);
    }

    [Fact]
    public void AddRegistryHttpParticipation_RejectsEmbeddedCredentialsWithoutEchoingThem()
    {
        var settings = new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "http://user:secret@gateway.internal/" };
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(settings).AddRegistryHttpParticipation());
        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddRegistryHttpParticipation_IsIdempotent()
    {
        var builder = Builder(Gateway);
        builder.AddRegistryHttpParticipation().AddRegistryHttpParticipation();

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Single(provider.GetServices<IHostedService>(), service => service.GetType().Name == "ControlRegistryRegistrar");
        Assert.Single(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations);
    }

    [Fact]
    public void AddRegistryHttpParticipation_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddRegistryHttpParticipation());
        Assert.Throws<ArgumentNullException>(() => builder!.AddRegistryHttpClient());
    }
}
