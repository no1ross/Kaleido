using System.Reflection;
using Kaleido.Client.Processor;
using Kaleido.Client.Queryable;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registration.UnitTests;

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

    // The configured clients' definitions, by name.
    private static Dictionary<string, KaleidoClientDefinition> Definitions(IServiceCollection services) =>
        services.Select(d => d.ImplementationInstance).OfType<KaleidoClientDefinition>().ToDictionary(x => x.Name, StringComparer.Ordinal);

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
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IKaleidoRegistrySource));
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
            ["Kaleido:Clients:Member:BaseUrl"] = "http://member/",
            ["Kaleido:Clients:Radiology:RoutePrefix"] = "imaging",
        });

        builder.AddHttpClients();

        var clients = Definitions(builder.Services);
        Assert.Equal("member", clients["Member"].RoutePrefix);
        Assert.Equal("imaging", clients["Radiology"].RoutePrefix);
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

        var member = Definitions(builder.Services)["Member"];
        using var provider = Build(builder.Services);
        Assert.Equal("member", member.RoutePrefix);
        Assert.Equal(new Uri("http://router/"), provider.GetRequiredService<IHttpClientFactory>().CreateClient("Member").BaseAddress);
    }

    // #521: a BaseUrl that is not an absolute http(s) URL fails at startup with the client's name and the
    // key it came from, not a raw UriFormatException when the HTTP client is built.
    [Theory]
    [InlineData("Kaleido:Clients:Member:BaseUrl", "/member", "Kaleido:Clients:Member:BaseUrl")]
    [InlineData("Kaleido:Clients:Member:BaseUrl", "member:8080", "Kaleido:Clients:Member:BaseUrl")]
    [InlineData("Kaleido:Clients:Member:BaseUrl", "ftp://member", "Kaleido:Clients:Member:BaseUrl")]
    [InlineData("Kaleido:Clients:Member:BaseUrl", "http://", "Kaleido:Clients:Member:BaseUrl")]
    [InlineData("Kaleido:BaseUrl", "router", "Kaleido:BaseUrl")]
    public void AddHttpClients_ABaseUrlThatIsNotAnAbsoluteHttpUrl_ThrowsAConfigurationError(string setting, string value, string key)
    {
        var builder = CreateBuilder(new() { [setting] = value, ["Kaleido:Clients:Member:RoutePrefix"] = "member" });

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddHttpClients);

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("'Member'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://member:8080")]
    [InlineData("https://member.example/")]
    public void AddHttpClients_AnAbsoluteHttpBaseUrl_IsAccepted(string value)
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = value });

        builder.AddHttpClients();

        Assert.True(Definitions(builder.Services).ContainsKey("Member"));
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
    [InlineData("Kaleido:Resilience:MaxRetryAttempts", "-1")]
    [InlineData("Kaleido:Clients:Member:Resilience:TotalTimeout", "00:00:00")]
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
    public void AddHttpClients_WithClients_DoesNotReplaceTheRegistrySource_AndAddsTheCredentialFilterOnce()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });
        builder.Services.AddScoped(_ => Mock.Of<IKaleidoRegistrySource>());

        builder.AddHttpClients();
        builder.AddHttpClients();

        var source = Assert.Single(builder.Services, d => d.ServiceType == typeof(IKaleidoRegistrySource));
        Assert.Null(source.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, source.Lifetime);
        Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(IHttpMessageHandlerBuilderFilter) && d.ImplementationType == typeof(KaleidoClientHandlerFilter));
    }

    // #574: a host handler is registered once per type, in the order added, with the pipeline filter.
    [Fact]
    public void AddKaleidoClientHandler_RegistersEachHandlerOnce_InTheOrderAdded()
    {
        var builder = CreateBuilder(new() { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" });

        var result = builder
            .AddKaleidoClientHandler<FirstHandler>()
            .AddKaleidoClientHandler<SecondHandler>()
            .AddKaleidoClientHandler<FirstHandler>();

        Assert.Same(builder, result);
        Assert.Equal(
            [typeof(FirstHandler), typeof(SecondHandler)],
            builder.Services.Select(d => d.ImplementationInstance).OfType<KaleidoClientHandlerRegistration>().Select(x => x.HandlerType));
        var handler = Assert.Single(builder.Services, d => d.ServiceType == typeof(FirstHandler));
        Assert.Equal(ServiceLifetime.Transient, handler.Lifetime);
        Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(IHttpMessageHandlerBuilderFilter) && d.ImplementationType == typeof(KaleidoClientHandlerFilter));
    }

    [Theory]
    [InlineData(null, KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Forward", KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Exchange", KaleidoOnBehalfOfCalls.Exchange)]
    public void AddHttpClients_ReadsOnBehalfOfCalls_ForwardByDefault(string? configured, KaleidoOnBehalfOfCalls expected)
    {
        var settings = new Dictionary<string, string?> { ["Kaleido:Clients:Member:BaseUrl"] = "http://member/" };

        if (configured is not null)
        {
            settings["Kaleido:OnBehalfOfCalls"] = configured;
        }

        var builder = CreateBuilder(settings);

        builder.AddHttpClients();

        Assert.Equal(expected, builder.Services.Select(d => d.ImplementationInstance).OfType<KaleidoClientOptions>().Single().OnBehalfOfCalls);
    }

    private sealed class FirstHandler : DelegatingHandler;

    private sealed class SecondHandler : DelegatingHandler;

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

    private sealed class FakeKaleidoBuilder(IServiceCollection services, IConfiguration configuration) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;
        public IReadOnlyCollection<Assembly> Assemblies { get; } = [typeof(FakeKaleidoBuilder).Assembly];
        public IConfiguration Configuration { get; } = configuration;
        public KaleidoServiceOptions ServiceOptions { get; } = new() { ServiceName = "router" };
    }
}
