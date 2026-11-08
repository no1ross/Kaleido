using Kaleido.Exceptions;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registry.UnitTests;

// #553: a host taking part in the control registry reads its registry from it.
public sealed class KaleidoControlRegistryClientServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly Dictionary<string, string?> Registry = new() { ["Kaleido:BaseUrl"] = "https://gateway.internal" };

    private static IKaleidoBuilder CreateBuilder(Dictionary<string, string?> settings, string? version = "3")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>());
        services.AddSingleton(new KaleidoServiceOptions { ServiceName = "code-sets", ServiceVersion = version });

        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        builder.Setup(x => x.ServiceOptions).Returns(new KaleidoServiceOptions { ServiceName = "code-sets", ServiceVersion = version });
        builder.Setup(x => x.Assemblies).Returns([typeof(KaleidoControlRegistryClientServiceCollectionExtensionsTests).Assembly]);
        return builder.Object;
    }

    [Fact]
    public void AddControlRegistryClient_MakesTheControlRegistrysRegistry_ThisHostsSource()
    {
        var builder = CreateBuilder(Registry);

        builder.AddControlRegistryClient();

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var source = Assert.IsType<CentralRegistrySource>(provider.GetRequiredService<IKaleidoRegistrySource>());
        Assert.Same(source, provider.GetRequiredService<ICentralRegistrySource>());
        using var scope = provider.CreateScope();
        Assert.IsType<KaleidoHttpRegistryClient>(scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>());
        Assert.Contains(provider.GetServices<IHostedService>(), x => x is CentralRegistrySync);
        var registrar = Assert.Single(provider.GetServices<IHostedService>().OfType<ControlRegistryRegistrar>());
        Assert.Same(registrar, provider.GetRequiredService<IControlRegistryRegistrar>());
        var check = Assert.Single(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations, x => x.Name == RegistrationHealthCheck.Name);
        Assert.Contains("ready", check.Tags);
        Assert.Contains(provider.GetServices<IHttpMessageHandlerBuilderFilter>(), x => x is KaleidoClientHandlerFilter);
        Assert.Equal(new Uri("https://gateway.internal/"), provider.GetRequiredService<IHttpClientFactory>().CreateClient(KaleidoHttpRegistryClient.ClientName).BaseAddress);
    }

    // Clients configured too: the control registry's registry stays the full one, whichever call came first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithClients_TheControlRegistrysSource_IsKept(bool registryFirst)
    {
        var builder = CreateBuilder(new Dictionary<string, string?>(Registry) { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });

        if (registryFirst)
        {
            builder.AddControlRegistryClient().AddHttpClients();
        }
        else
        {
            builder.AddHttpClients().AddControlRegistryClient();
        }

        var source = Assert.Single(builder.Services, x => x.ServiceType == typeof(IKaleidoRegistrySource));
        Assert.Null(source.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, source.Lifetime);
    }

    [Fact]
    public void AddControlRegistryClient_WithoutAGatewayBaseUrl_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder([]).AddControlRegistryClient());

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddControlRegistryClient_WhenBuilderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IKaleidoBuilder)null!).AddControlRegistryClient());
    }

    // Every registered service names its version: without one the registry would refuse every registration.
    [Fact]
    public void AddControlRegistryClient_WithoutAServiceVersion_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder(Registry, version: null).AddControlRegistryClient());

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:ServiceVersion", exception.Message, StringComparison.Ordinal);
    }

    // Called twice: one registrar, one readiness check.
    [Fact]
    public void AddControlRegistryClient_Twice_RegistersOnce()
    {
        var builder = CreateBuilder(Registry);

        builder.AddControlRegistryClient().AddControlRegistryClient();

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IHostedService>().OfType<ControlRegistryRegistrar>());
        Assert.Single(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations, x => x.Name == RegistrationHealthCheck.Name);
    }
}
