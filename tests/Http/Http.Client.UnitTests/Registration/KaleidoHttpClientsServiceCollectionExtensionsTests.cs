using System.Reflection;
using Kaleido.Client.Processor;
using Kaleido.Client.Queryable;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registration.UnitTests;

public sealed class KaleidoHttpClientsServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    private static FakeKaleidoBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>());
        services.AddSingleton<IKaleidoRegistrySource>(Mock.Of<IKaleidoRegistrySource>());
        services.AddSingleton(new KaleidoServiceOptions { ServiceName = "router" });
        return new FakeKaleidoBuilder(services, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact]
    public void AddHttpClients_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddHttpClients());
    }

    [Fact]
    public void AddHttpClients_RegistersOneGatewayAndBothFactories()
    {
        var builder = CreateBuilder(new() { ["Kaleido:BaseUrl"] = "http://gateway.test/" });

        Assert.Same(builder, builder.AddHttpClients());
        builder.AddHttpClients();
        using var provider = Build(builder.Services);
        using var scope = provider.CreateScope();

        Assert.Equal(new Uri("http://gateway.test/"), provider.GetRequiredService<IHttpClientFactory>().CreateClient(KaleidoClientTransport.ClientName).BaseAddress);
        Assert.Null(provider.GetRequiredService<IHttpClientFactory>().CreateClient("radiology").BaseAddress);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("radiology"));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient("radiology"));
        Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<IKaleidoProcessorClient>("radiology"));
        Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<IKaleidoQueryableClient>("radiology"));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IKaleidoRegistrySource));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IHttpMessageHandlerBuilderFilter) && x.ImplementationType == typeof(KaleidoClientHandlerFilter));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && x.ImplementationType == typeof(KaleidoClientTransportCheck));
    }

    [Fact]
    public void AddHttpClients_WithoutAGatewayBaseUrl_FailsAtRegistration()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder([]).AddHttpClients());

        Assert.Equal(HttpClientErrorCodes.MissingBaseUrl, exception.Code);
        Assert.Contains("Kaleido:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("ftp://gateway.test/")]
    [InlineData("http://")]
    [InlineData("http://user:secret@gateway.test/")]
    public void AddHttpClients_RejectsAnInvalidGatewayUrl(string address)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder(new() { ["Kaleido:BaseUrl"] = address }).AddHttpClients());

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHttpClients_RejectsAnOldPerServiceEndpointList()
    {
        var builder = CreateBuilder(new() { ["Kaleido:BaseUrl"] = "http://gateway.test/", ["Kaleido:Clients:Member:BaseUrl"] = "http://member.test/" });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:Clients", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Kaleido:Resilience:MaxRetryAttempts", "-1")]
    [InlineData("Kaleido:Resilience:TotalTimeout", "00:00:00")]
    public void AddHttpClients_AnInvalidResilienceSetting_StopsStartup(string setting, string value)
    {
        var builder = CreateBuilder(new() { ["Kaleido:BaseUrl"] = "http://gateway.test/", [setting] = value });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddKaleidoClientHandler_RegistersEachHandlerOnce_InTheOrderAdded()
    {
        var builder = CreateBuilder([]);

        var result = builder.AddKaleidoClientHandler<FirstHandler>().AddKaleidoClientHandler<SecondHandler>().AddKaleidoClientHandler<FirstHandler>();

        Assert.Same(builder, result);
        Assert.Equal([typeof(FirstHandler), typeof(SecondHandler)],
            builder.Services.Select(x => x.ImplementationInstance).OfType<KaleidoClientHandlerRegistration>().Select(x => x.HandlerType));
        Assert.Equal(ServiceLifetime.Transient, Assert.Single(builder.Services, x => x.ServiceType == typeof(FirstHandler)).Lifetime);
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IHttpMessageHandlerBuilderFilter) && x.ImplementationType == typeof(KaleidoClientHandlerFilter));
    }

    [Theory]
    [InlineData(null, KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Forward", KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Exchange", KaleidoOnBehalfOfCalls.Exchange)]
    public void AddHttpClients_ReadsOnBehalfOfCalls(string? configured, KaleidoOnBehalfOfCalls expected)
    {
        var settings = new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "http://gateway.test/" };
        if (configured is not null)
        {
            settings["Kaleido:OnBehalfOfCalls"] = configured;
        }

        var builder = CreateBuilder(settings);
        builder.AddHttpClients();

        Assert.Equal(expected, Assert.Single(builder.Services.Select(x => x.ImplementationInstance).OfType<KaleidoClientOptions>()).OnBehalfOfCalls);
    }

    private sealed class FirstHandler : DelegatingHandler;
    private sealed class SecondHandler : DelegatingHandler;

    private sealed class FakeKaleidoBuilder(IServiceCollection services, IConfiguration configuration) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;
        public IReadOnlyCollection<Assembly> Assemblies { get; } = [typeof(FakeKaleidoBuilder).Assembly];
        public IConfiguration Configuration { get; } = configuration;
        public KaleidoServiceOptions ServiceOptions { get; } = new() { ServiceName = "router" };
    }
}
