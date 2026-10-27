using System.Reflection;
using Kaleido.Exceptions;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoHttpClientsServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static FakeKaleidoBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>());
        services.AddSingleton(new KaleidoServiceOptions { ServiceName = "router" });

        return new FakeKaleidoBuilder(
            services,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static T Instance<T>(IServiceCollection services)
        where T : class =>
        (T)Assert.Single(services, d => d.ServiceType == typeof(T)).ImplementationInstance!;

    [Fact]
    public void AddHttpClients_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddHttpClients());
    }

    [Fact]
    public void AddHttpClients_WithoutClients_RegistersOnlyTheBoundOptions()
    {
        var builder = CreateBuilder(new() { ["Kaleido:BaseUrl"] = "http://router/" });

        var result = builder.AddHttpClients();

        Assert.Same(builder, result);
        var options = Instance<KaleidoClientOptions>(builder.Services);
        Assert.Equal("http://router/", options.BaseUrl);
        Assert.Empty(options.Clients);
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IKaleidoDownstreamRegistries));
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IHttpMessageHandlerBuilderFilter));
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IKaleidoProcessorClientFactory));
        Assert.DoesNotContain(builder.Services, d => d.ImplementationType == typeof(KaleidoClientTransportCheck));
    }

    [Fact]
    public void AddHttpClients_RegistersAProcessorAndAQueryableClientPerConfiguredClient()
    {
        var builder = CreateBuilder(new()
        {
            ["Kaleido:BaseUrl"] = "http://router/",
            ["Kaleido:RegistryTtl"] = "00:02:00",
            ["Kaleido:Clients:Member:BaseUrl"] = "http://member/",
            ["Kaleido:Clients:Radiology:RoutePrefix"] = "imaging",
            ["Kaleido:Clients:Radiology:RegistryTtl"] = "00:00:05",
        });

        builder.AddHttpClients();

        var processMap = Instance<KaleidoProcessorClientRouteOptionsMap>(builder.Services);
        var queryableMap = Instance<KaleidoQueryableClientRouteOptionsMap>(builder.Services);
        Assert.Equal("member", processMap.Options["Member"]);
        Assert.Equal("imaging", processMap.Options["Radiology"]);
        Assert.Equal("member", queryableMap.Options["Member"]);
        Assert.Equal("imaging", queryableMap.Options["Radiology"]);
        Assert.Equal(TimeSpan.FromMinutes(2), processMap.RegistryTtls["Member"]);
        Assert.Equal(TimeSpan.FromSeconds(5), processMap.RegistryTtls["Radiology"]);
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IKaleidoProcessorClientFactory));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IKaleidoQueryableClientFactory));
    }

    [Fact]
    public void AddHttpClients_AnEmptyClientEntry_UsesTheSharedSettings()
    {
        var builder = CreateBuilder(new()
        {
            ["Kaleido:BaseUrl"] = "http://router/",
            ["Kaleido:Clients:Member"] = null,
        });

        builder.AddHttpClients();

        var processMap = Instance<KaleidoProcessorClientRouteOptionsMap>(builder.Services);
        Assert.Equal("member", processMap.Options["Member"]);
        Assert.Equal(TimeSpan.FromSeconds(30), processMap.RegistryTtls["Member"]);
    }

    [Fact]
    public void AddHttpClients_AClientWithoutAnyBaseUrl_ThrowsAConfigurationError()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:RoutePrefix"] = "member" });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.MissingBaseUrl, exception.Code);
        Assert.Contains("Kaleido:Clients:Member:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    // ADR 0030: out-of-range or unreadable client settings stop startup; nothing is adjusted.
    [Theory]
    [InlineData("Kaleido:RegistryTtl", "-00:00:01")]
    [InlineData("Kaleido:Resilience:MaxRetryAttempts", "-1")]
    [InlineData("Kaleido:Clients:Member:Resilience:TotalTimeout", "00:00:00")]
    [InlineData("Kaleido:Clients:Member:RegistryTtl", "often")]
    public void AddHttpClients_AnInvalidSetting_ThrowsAConfigurationError(string key, string value)
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/", [key] = value });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    // #434: the route prefix is the downstream ServiceName in every route; anything else fails at startup.
    [Theory]
    [InlineData("Kaleido:Clients:Member:RoutePrefix", "Member")]
    [InlineData("Kaleido:Clients:Member:RoutePrefix", "../admin")]
    [InlineData("Kaleido:Clients:Member:RoutePrefix", "member/x")]
    [InlineData("Kaleido:Clients:my.member:BaseUrl", "http://member/")]
    public void AddHttpClients_ARoutePrefixThatIsNotAServiceName_ThrowsAConfigurationError(string key, string value)
    {
        var builder = CreateBuilder(new() { ["Kaleido:BaseUrl"] = "http://router/", [key] = value });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.InvalidRoutePrefix, exception.Code);
        Assert.Contains(":RoutePrefix", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHttpClients_WithClients_RegistersTheTransportCheckOnce()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });

        builder.AddHttpClients();
        builder.AddHttpClients();

        Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && d.ImplementationType == typeof(KaleidoClientTransportCheck));
    }

    [Fact]
    public void AddHttpClients_WithClients_ReplacesTheDownstreamRegistriesAndAddsTheCredentialFilterOnce()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });
        builder.Services.AddScoped(_ => Mock.Of<IKaleidoDownstreamRegistries>());

        builder.AddHttpClients();
        builder.AddHttpClients();

        var downstream = Assert.Single(builder.Services, d => d.ServiceType == typeof(IKaleidoDownstreamRegistries));
        Assert.Equal(typeof(ClientDownstreamRegistries), downstream.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, downstream.Lifetime);
        Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(IHttpMessageHandlerBuilderFilter) && d.ImplementationType == typeof(KaleidoCallCredentialHandlerFilter));
    }

    // ADR 0022: one provider decides every call's credential; a later registration replaces an earlier one.
    [Fact]
    public void AddCallCredentials_RegistersTheProviderAsASingleton_ReplacingAnEarlierOne()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });

        var result = builder.ForwardCallerToken().AddCallCredentials<TestCallCredentialProvider>();

        Assert.Same(builder, result);
        var provider = Assert.Single(builder.Services, d => d.ServiceType == typeof(IKaleidoCallCredentialProvider));
        Assert.Equal(typeof(TestCallCredentialProvider), provider.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, provider.Lifetime);
    }

    [Fact]
    public void ForwardCallerToken_RegistersTheBuiltInPassThrough()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });

        builder.ForwardCallerToken();

        Assert.Equal(typeof(ForwardCallerTokenProvider), Assert.Single(builder.Services, d => d.ServiceType == typeof(IKaleidoCallCredentialProvider)).ImplementationType);
    }

    private sealed class TestCallCredentialProvider : IKaleidoCallCredentialProvider
    {
        public ValueTask<KaleidoCallCredential> GetCredentialAsync(KaleidoCallCredentialRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(KaleidoCallCredential.None);
    }

    [Fact]
    public void AddHttpClients_AClientUsesItsOwnBaseUrl_OrTheSharedOne()
    {
        var builder = CreateBuilder(new()
        {
            ["Kaleido:BaseUrl"] = "http://router/",
            ["Kaleido:Clients:Member:RoutePrefix"] = "member",
            ["Kaleido:Clients:Radiology:BaseUrl"] = "http://radiology/",
        });

        builder.AddHttpClients();
        using var provider = Build(builder.Services);
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(new Uri("http://router/"), factory.CreateClient("Member").BaseAddress);
        Assert.Equal(new Uri("http://radiology/"), factory.CreateClient("Radiology").BaseAddress);
    }

    [Fact]
    public async Task AddHttpClients_TheHealthCheckProbe_FollowsTheSharedOrTheClientsStrictSetting()
    {
        var builder = CreateBuilder(new()
        {
            ["Kaleido:BaseUrl"] = "http://router/",
            ["Kaleido:StrictRegistryProbe"] = "true",
            ["Kaleido:Clients:Member:RoutePrefix"] = "member",
            ["Kaleido:Clients:Radiology:StrictRegistryProbe"] = "false",
        });
        builder.AddHttpClients();
        var wire = new RecordingHandler();
        builder.Services.AddHttpClient("Member").ConfigurePrimaryHttpMessageHandler(() => wire);
        builder.Services.AddHttpClient("Radiology").ConfigurePrimaryHttpMessageHandler(() => wire);
        using var provider = Build(builder.Services);

        foreach (var registration in provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations)
        {
            var result = await registration.Factory(provider).CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        Assert.Equal(["/member/registry?strict", "/radiology/registry"], wire.Paths.Order());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Paths)
            {
                Paths.Add(request.RequestUri!.PathAndQuery);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeKaleidoBuilder(IServiceCollection services, IConfiguration configuration) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;
        public IReadOnlyCollection<Assembly> Assemblies { get; } = [typeof(FakeKaleidoBuilder).Assembly];
        public IConfiguration Configuration { get; } = configuration;
        public KaleidoServiceOptions ServiceOptions { get; } = new() { ServiceName = "router" };
    }
}
