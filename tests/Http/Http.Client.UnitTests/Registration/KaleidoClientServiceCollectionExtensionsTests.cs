using System.Reflection;
using Kaleido.Client.Processor;
using Kaleido.Client.Queryable;
using Kaleido.Http.Client.Queryable;
using Kaleido.Observability;
using Kaleido.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Client.Registration.UnitTests;

public sealed class KaleidoClientServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    // ── Guard clauses ────────────────────────────────────────────────────────────

    [Fact]
    public void AddKaleidoClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddKaleidoClient(new() { Name = "name", BaseUrl = "http://localhost" }));
    }

    [Fact]
    public void AddKaleidoClient_WhenOptionsAreNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() => builder.AddKaleidoClient(null!));
    }

    [Theory]
    [InlineData("", "http://localhost")]
    [InlineData("name", "")]
    public void AddKaleidoClient_WithoutANameOrABaseUrl_Throws(string name, string baseUrl)
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(() => builder.AddKaleidoClient(new() { Name = name, BaseUrl = baseUrl }));
    }

    // ── What one client registers ────────────────────────────────────────────────

    [Fact]
    public void AddKaleidoClient_ReturnsTheSameBuilder()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Same(builder, builder.AddKaleidoClient(new() { Name = "Radiology", BaseUrl = "http://localhost" }));
    }

    [Fact]
    public void AddKaleidoClient_RegistersTheClientsDefinition()
    {
        var services = new ServiceCollection();

        new FakeKaleidoBuilder(services).AddKaleidoClient(new()
        {
            Name = "Radiology",
            BaseUrl = "http://localhost",
            RoutePrefix = "imaging",
            Resilience = new KaleidoResilienceOptions { TotalTimeout = TimeSpan.FromSeconds(2) },
        });

        Assert.Equal(
            new KaleidoClientDefinition("Radiology", "imaging"),
            Assert.Single(Definitions(services)));
    }

    // AddHttpClients() may run more than once: a name already registered is left as it is.
    [Fact]
    public void AddKaleidoClient_ANameRegisteredAgainInAnyCase_IsANoOp()
    {
        var services = new ServiceCollection();

        new FakeKaleidoBuilder(services)
            .AddKaleidoClient(new() { Name = "Radiology", BaseUrl = "http://localhost", RoutePrefix = "radiology" })
            .AddKaleidoClient(new() { Name = "radiology", BaseUrl = "http://other", RoutePrefix = "other" });

        Assert.Equal("radiology", Assert.Single(Definitions(services)).RoutePrefix);
    }

    [Fact]
    public void AddKaleidoClient_RegistersTheSharedServicesOnce()
    {
        var services = new ServiceCollection();

        new FakeKaleidoBuilder(services)
            .AddKaleidoClient(new() { Name = "Radiology", BaseUrl = "http://localhost" })
            .AddKaleidoClient(new() { Name = "Member", BaseUrl = "http://localhost" });

        Assert.All(
            new[] { typeof(IKaleidoClientCatalog), typeof(IKaleidoClientTransport), typeof(IKaleidoProcessorClientFactory), typeof(IKaleidoQueryableClientFactory) },
            type => Assert.Single(services, d => d.ServiceType == type));
    }

    [Fact]
    public void AddKaleidoClient_InvokesTheCallbackWithTheHttpClientBuilder()
    {
        IHttpClientBuilder? configured = null;

        new FakeKaleidoBuilder(new ServiceCollection()).AddKaleidoClient(
            new() { Name = "Radiology", BaseUrl = "http://localhost" },
            http => configured = http);

        Assert.Equal("Radiology", configured?.Name);
    }

    // The container builds each client's services, keyed by the client's name (#498).
    [Fact]
    public void AddKaleidoClient_TheClientsAreResolvedByName_FromTheCatalog()
    {
        var services = ClientServices();
        new FakeKaleidoBuilder(services)
            .AddKaleidoClient(new() { Name = "Radiology", BaseUrl = "http://radiology/", RoutePrefix = "imaging" })
            .AddKaleidoClient(new() { Name = "Member", BaseUrl = "http://member/" });
        using var provider = Build(services);
        using var scope = provider.CreateScope();

        Assert.Equal(["Member", "Radiology"], scope.ServiceProvider.GetRequiredService<IKaleidoClientCatalog>().Clients.Select(x => x.Name).Order());
        Assert.IsType<KaleidoProcessorClient>(scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("radiology"));
        Assert.IsType<KaleidoQueryableClient>(scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient("Member"));
        Assert.IsType<KaleidoProcessorClient>(scope.ServiceProvider.GetRequiredKeyedService<IKaleidoProcessorClient>("Radiology"));
    }

    // ── Resilience and health check ──────────────────────────────────────────────

    private static readonly KaleidoResilienceOptions FastResilience = new()
    {
        MaxRetryAttempts = 2,
        RetryDelay = TimeSpan.FromMilliseconds(1),
    };

    private static ServiceCollection ClientServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>());
        services.AddSingleton(new KaleidoServiceOptions { ServiceName = "test" });
        return services;
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static Task<HttpResponseMessage> GetAsync(ServiceProvider provider, string clientName, string path) =>
        provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName).GetAsync(path, TestContext.Current.CancellationToken);

    private static KaleidoClientDefinition[] Definitions(IServiceCollection services) =>
        [.. services.Select(d => d.ImplementationInstance).OfType<KaleidoClientDefinition>()];

    [Fact]
    public async Task AddKaleidoClient_WithResilience_RetriesATransientReadFailure()
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services).AddKaleidoClient(
            new()
            {
                Name = "Radiology",
                BaseUrl = "http://radiology/",
                Resilience = FastResilience,
            },
            http => http.ConfigurePrimaryHttpMessageHandler(() => wire));
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/processes/steps/startstep");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, wire.Paths.Count);
    }

    // A pipeline stacked by a second registration of the name would make 3 x 3 attempts.
    [Fact]
    public async Task AClientRegisteredTwice_HasOneResiliencePipeline()
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services)
            .AddKaleidoClient(
                new()
                {
                    Name = "Radiology",
                    BaseUrl = "http://radiology/",
                    Resilience = FastResilience,
                },
                http => http.ConfigurePrimaryHttpMessageHandler(() => wire))
            .AddKaleidoClient(new()
            {
                Name = "radiology",
                BaseUrl = "http://radiology/",
                Resilience = FastResilience,
            });
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/processes/steps/startstep");

        Assert.Equal(3, wire.Paths.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddKaleidoClient_WithoutEnabledResilience_CallsOnce(bool disabled)
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services).AddKaleidoClient(
            new()
            {
                Name = "Radiology",
                BaseUrl = "http://radiology/",
                Resilience = disabled ? new KaleidoResilienceOptions { Enabled = false } : null,
            },
            http => http.ConfigurePrimaryHttpMessageHandler(() => wire));
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/processes/steps/startstep");

        Assert.Single(wire.Paths);
    }

    [Fact]
    public void AddKaleidoClient_DoesNotProbeOtherServicesForHealth()
    {
        var services = ClientServices();
        services.AddHealthChecks();
        new FakeKaleidoBuilder(services).AddKaleidoClient(new() { Name = "Radiology", BaseUrl = "http://radiology/" });
        using var provider = Build(services);

        Assert.Empty(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────────

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Paths)
            {
                Paths.Add(request.RequestUri!.PathAndQuery);
            }

            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class FakeKaleidoBuilder(IServiceCollection services) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;
        public IReadOnlyCollection<Assembly> Assemblies { get; } = [typeof(FakeKaleidoBuilder).Assembly];
        public IConfiguration Configuration { get; } =
            new ConfigurationBuilder().Build();
        public KaleidoServiceOptions ServiceOptions { get; } =
            new() { ServiceName = "test" };
    }
}
