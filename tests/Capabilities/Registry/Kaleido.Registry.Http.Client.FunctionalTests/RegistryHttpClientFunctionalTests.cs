using System.Net.Http.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Registry.Http.Client.FunctionalTests;

public sealed class RegistryHttpClientFunctionalTests
{
    [Fact]
    public async Task FixedEndpointClient_RegistersReadsConditionallyRenewsAndDeregisters()
    {
        await using var authority = await StartAuthorityAsync();
        using var gatewayClient = new HttpClient(authority.GetTestServer().CreateHandler()) { BaseAddress = new Uri("http://localhost/") };
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://localhost/",
        }).Build();
        services.AddKaleido(configuration, new()
        {
            ServiceName = "consumer",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryHttpClientFunctionalTests).Assembly],
            TypeFilter = _ => false,
        }).AddRegistryHttpClient();
        services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("kaleido-registry", gatewayClient));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>();
        var instance = Guid.NewGuid();
        var registration = new RegistryInstanceRegistration
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            BuildVersion = "1.0.0-preview.616",
            InstanceId = instance,
            Queryables = [new QueryableRegistryItem
            {
                Name = "orders",
                ServiceName = "catalog",
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/orders/query" }],
                },
            }],
        };

        var lease = await client.RegisterAsync(registration, TestContext.Current.CancellationToken);
        Assert.Equal(instance, lease.InstanceId);
        var capabilities = await client.GetCapabilitiesAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal("orders", Assert.Single(Assert.IsType<KaleidoRegistry>(capabilities.Value).Queryables).Name);
        Assert.StartsWith("W/\"", capabilities.ETag, StringComparison.Ordinal);
        var unchanged = await client.GetCapabilitiesAsync(capabilities.ETag, TestContext.Current.CancellationToken);
        Assert.True(unchanged.NotModified);
        Assert.Null(unchanged.Value);
        var availability = await client.GetAvailabilityAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal(capabilities.Value.Revision, Assert.IsType<KaleidoRegistryAvailability>(availability.Value).CapabilitiesRevision);
        Assert.NotNull(await client.HeartbeatAsync(instance, new RegistryHeartbeat { Healthy = true }, TestContext.Current.CancellationToken));
        await client.DeregisterAsync(instance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReaderOnlyParticipant_HydratesWithoutAdvertisingAnEmptyService()
    {
        await using var authority = await StartAuthorityAsync();
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var gatewayClient = new HttpClient(new ObservedReadHandler(read)
        {
            InnerHandler = authority.GetTestServer().CreateHandler(),
        })
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://localhost/",
        });
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryHttpClientFunctionalTests).Assembly],
            TypeFilter = _ => false,
        }).AddRegistryHttpParticipation();
        builder.Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("kaleido-registry", gatewayClient));
        await using var participant = builder.Build();

        await participant.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var response = await authority.GetTestClient().GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var status = await response.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Empty(Assert.IsType<KaleidoRegistryStatus>(status).Services);
        await participant.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SharedNamedClient_UsesAServiceTokenForRegistrationAndUnfilteredReads()
    {
        await using var authority = await StartAuthorityAsync(enforceAuthorization: true);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        }).Build();
        services.AddKaleido(configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryHttpClientFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddKaleidoClientHandler<ServiceTokenHandler>()
            .AddRegistryHttpClient();
        services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = authority.GetTestServer().CreateHandler()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>();
        var id = Guid.NewGuid();

        var lease = await client.RegisterAsync(new RegistryInstanceRegistration
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            BuildVersion = "1.0.0-preview.616",
            InstanceId = id,
        }, TestContext.Current.CancellationToken);
        var capabilities = await client.GetCapabilitiesAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(id, lease.InstanceId);
        Assert.NotNull(capabilities.Value);
        Assert.StartsWith("W/\"", capabilities.ETag, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> StartAuthorityAsync(bool enforceAuthorization = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (enforceAuthorization)
        {
            builder.Services.AddTestJwtBearer();
            builder.Services.AddAuthorization(policies =>
            {
                policies.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
                policies.AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
            });
        }

        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryHttpClientFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = enforceAuthorization ? KaleidoAuthorizationMode.Authenticated : KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry(enforceAuthorization ? new KaleidoRegistryOptions { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" } : null)
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        if (enforceAuthorization)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapKaleidoControlRegistry();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class ServiceTokenHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(KaleidoCallPurpose.ServiceOwn, KaleidoCallContext.Of(request).Purpose);
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "registry-reader", "Kaleido.Registrant.catalog"]));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ObservedReadHandler(TaskCompletionSource<bool> observed) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (string.Equals(request.RequestUri?.AbsolutePath, "/registry/capabilities", StringComparison.Ordinal))
            {
                observed.TrySetResult(true);
            }

            return response;
        }
    }

    private sealed class FixedHttpClientFactory(string name, HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string requested) =>
            string.Equals(name, requested, StringComparison.Ordinal) ? client : throw new InvalidOperationException("Unknown test HTTP client.");
    }
}
