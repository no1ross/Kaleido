using System.Reflection;
using Kaleido.Http.Client.HealthChecks;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    // ── AddProcessorClient — guard clauses ────────────────────────────────────────

    [Fact]
    public void AddProcessClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddProcessorClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenConfigureIsNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddProcessorClient(null!));
    }

    [Fact]
    public void AddProcessClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessorClient(o =>
            {
                o.Name = "";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessorClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "";
            }));
    }

    // ── AddProcessorClient — factory registration ──────────────────────────────────

    [Fact]
    public void AddProcessClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoProcessorClientFactory));
    }

    [Fact]
    public void AddProcessClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        Assert.Same(builder, result);
    }

    // ── AddProcessorClient — RouteOptionsMap ───────────────────────────────────────

    [Fact]
    public void AddProcessClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RoutePrefix = "radiology";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var prefix));
        Assert.Equal("radiology", prefix);
    }

    [Fact]
    public void AddProcessClient_WithRegistryTtl_StoresTheTtlInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RegistryTtl = TimeSpan.FromMinutes(5);
        });

        var map = (KaleidoProcessorClientRouteOptionsMap)services
            .First(d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap))
            .ImplementationInstance!;

        Assert.Equal(TimeSpan.FromMinutes(5), map.RegistryTtls["radiology"]);
    }

    [Fact]
    public void AddProcessClient_WithoutRegistryTtl_StoresNoTtl()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
        });

        var map = (KaleidoProcessorClientRouteOptionsMap)services
            .First(d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap))
            .ImplementationInstance!;

        Assert.Empty(map.RegistryTtls);
    }

    [Fact]
    public void AddQueryableClient_WithRegistryTtl_StoresTheTtlInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "Member";
            o.BaseUrl = "http://localhost";
            o.RegistryTtl = TimeSpan.FromSeconds(30);
        });

        var map = (KaleidoQueryableClientRouteOptionsMap)services
            .First(d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap))
            .ImplementationInstance!;

        Assert.Equal(TimeSpan.FromSeconds(30), map.RegistryTtls["member"]);
    }

    [Fact]
    public void AddProcessClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddProcessorClient(o => { o.Name = "ProcessorA"; o.BaseUrl = "http://a.localhost"; })
            .AddProcessorClient(o => { o.Name = "ProcessorB"; o.BaseUrl = "http://b.localhost"; o.RoutePrefix = "prefix"; });

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoProcessorClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ProcessorA"));
        Assert.True(map.Options.ContainsKey("ProcessorB"));
    }

    [Fact]
    public void AddProcessClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var prefix));
        Assert.Equal("", prefix);
    }

    [Fact]
    public void AddProcessClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
                o.RoutePrefix = "kaleido";
            });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var stored));
        Assert.Equal("kaleido", stored);
    }

    [Fact]
    public void AddProcessClient_WithConfigureClient_InvokesCallbackWithHttpClientBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);
        var callbackInvoked = false;

        builder.AddProcessorClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
            },
            http =>
            {
                Assert.NotNull(http);
                callbackInvoked = true;
            });

        Assert.True(callbackInvoked);
    }

    // ── AddQueryableClient — guard clauses ───────────────────────────────────────

    [Fact]
    public void AddQueryableClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddQueryableClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddQueryableClient_WhenConfigureIsNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddQueryableClient(null!));
    }

    [Fact]
    public void AddQueryableClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient(o =>
            {
                o.Name = "";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddQueryableClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "";
            }));
    }

    // ── AddQueryableClient — factory registration ────────────────────────────────

    [Fact]
    public void AddQueryableClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoQueryableClientFactory));
    }

    [Fact]
    public void AddQueryableClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        Assert.Same(builder, result);
    }

    // ── AddQueryableClient — RouteOptionsMap ─────────────────────────────────────

    [Fact]
    public void AddQueryableClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RoutePrefix = "radiology";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var prefix));
        Assert.Equal("radiology", prefix);
    }

    [Fact]
    public void AddQueryableClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddQueryableClient(o => { o.Name = "ServiceA"; o.BaseUrl = "http://a.localhost"; })
            .AddQueryableClient(o => { o.Name = "ServiceB"; o.BaseUrl = "http://b.localhost"; o.RoutePrefix = "prefix"; });

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoQueryableClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ServiceA"));
        Assert.True(map.Options.ContainsKey("ServiceB"));
    }

    [Fact]
    public void AddQueryableClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("MemberService", out var prefix));
        Assert.Equal("", prefix);
    }

    [Fact]
    public void AddQueryableClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(
            o =>
            {
                o.Name = "MemberService";
                o.BaseUrl = "http://localhost";
                o.RoutePrefix = "kaleido";
            });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("MemberService", out var stored));
        Assert.Equal("kaleido", stored);
    }

    [Fact]
    public void AddQueryableClient_WithConfigureClient_InvokesCallbackWithHttpClientBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);
        var callbackInvoked = false;

        builder.AddQueryableClient(
            o =>
            {
                o.Name = "MemberService";
                o.BaseUrl = "http://localhost";
            },
            http =>
            {
                Assert.NotNull(http);
                callbackInvoked = true;
            });

        Assert.True(callbackInvoked);
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

    [Fact]
    public async Task AddProcessorClient_WithResilience_RetriesATransientReadFailure()
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services).AddProcessorClient(
            o =>
            {
                o.Name = "Radiology";
                o.BaseUrl = "http://radiology/";
                o.Resilience = FastResilience;
            },
            http => http.ConfigurePrimaryHttpMessageHandler(() => wire));
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/registry");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, wire.Paths.Count);
    }

    // Two stacked pipelines would make 3 x 3 attempts.
    [Fact]
    public async Task AProcessorAndAQueryableClientOfOneName_ShareOneResiliencePipeline()
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services)
            .AddProcessorClient(
                o =>
                {
                    o.Name = "Radiology";
                    o.BaseUrl = "http://radiology/";
                    o.Resilience = FastResilience;
                },
                http => http.ConfigurePrimaryHttpMessageHandler(() => wire))
            .AddQueryableClient(o =>
            {
                o.Name = "radiology";
                o.BaseUrl = "http://radiology/";
                o.Resilience = FastResilience;
            });
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/registry");

        Assert.Equal(3, wire.Paths.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddProcessorClient_WithoutEnabledResilience_CallsOnce(bool disabled)
    {
        var wire = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var services = ClientServices();
        new FakeKaleidoBuilder(services).AddProcessorClient(
            o =>
            {
                o.Name = "Radiology";
                o.BaseUrl = "http://radiology/";
                o.Resilience = disabled ? new KaleidoResilienceOptions { Enabled = false } : null;
            },
            http => http.ConfigurePrimaryHttpMessageHandler(() => wire));
        using var provider = Build(services);

        using var response = await GetAsync(provider, "Radiology", "/radiology/registry");

        Assert.Single(wire.Paths);
    }

    [Fact]
    public void AddProcessorClient_RegistersOneRemoteHealthCheckPerClientName()
    {
        var services = ClientServices();
        new FakeKaleidoBuilder(services)
            .AddProcessorClient(o => { o.Name = "Radiology"; o.BaseUrl = "http://radiology/"; })
            .AddQueryableClient(o => { o.Name = "radiology"; o.BaseUrl = "http://radiology/"; });
        using var provider = Build(services);

        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);

        Assert.Equal("kaleido-Radiology", registration.Name);
        Assert.Equal(HealthStatus.Unhealthy, registration.FailureStatus);
        Assert.Equal(["kaleido", "remote"], registration.Tags.Order());
    }

    [Theory]
    [InlineData(false, "/radiology/registry")]
    [InlineData(true, "/radiology/registry?strict")]
    public async Task TheClientHealthCheck_ProbesTheRemoteRegistry_ThroughTheNamedClient(bool strict, string expectedPath)
    {
        var wire = new StubHandler(HttpStatusCode.OK);
        var services = ClientServices();
        new FakeKaleidoBuilder(services).AddProcessorClient(
            o =>
            {
                o.Name = "Radiology";
                o.BaseUrl = "http://radiology/";
                o.RoutePrefix = "radiology";
                o.StrictRegistryProbe = strict;
                o.Resilience = FastResilience;
            },
            http => http.ConfigurePrimaryHttpMessageHandler(() => wire));
        using var provider = Build(services);
        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);

        var check = registration.Factory(provider);
        var result = await check.CheckHealthAsync(new HealthCheckContext { Registration = registration }, TestContext.Current.CancellationToken);

        Assert.IsType<KaleidoClientHealthCheck>(check);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(expectedPath, Assert.Single(wire.Paths));
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
