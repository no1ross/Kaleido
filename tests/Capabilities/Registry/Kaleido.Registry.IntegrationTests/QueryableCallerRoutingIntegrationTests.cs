using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;

namespace Kaleido.Registry.IntegrationTests;

public sealed class QueryableCallerRoutingIntegrationTests
{
    private const string CatalogAudience = "catalog-api";

    [Fact]
    public async Task HydratedQueryableClient_ExchangesTheCallerTokenForThePublishedSource()
    {
        await using var authority = await StartAuthorityAsync();
        await using var publisher = await StartPublisherAsync();
        await PublishAsync(authority);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var proxy = await StartProxyAsync(authority, publisher, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(proxy);
        using var client = proxy.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/reader/1/queryable/forwardedcatalogsource/query")
        {
            Content = JsonContent.Create(new { query = new { } }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["radiology"]));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("available", body.GetProperty("results")[0].GetProperty("name").GetString());
        await proxy.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task PublishAsync(WebApplication authority)
    {
        using var client = authority.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(new RegistryInstanceRegistration
            {
                ServiceName = "catalog",
                ServiceVersion = "1",
                BuildVersion = "1.0.0-preview.616",
                InstanceId = Guid.NewGuid(),
                Queryables = [new QueryableRegistryItem
                {
                    ServiceName = "catalog",
                    Name = "ProtectedCatalogSource",
                    Authorization = new AuthorizationMetadata(null, ["radiology"]),
                    Bindings = new RegistryTransportBindings
                    {
                        Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/protectedcatalogsource/query" }],
                    },
                }],
            }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddAuthorization(policies =>
        {
            policies.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
            policies.AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
        });
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryableCallerRoutingIntegrationTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleidoControlRegistry();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartPublisherAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer(CatalogAudience);
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryableCallerRoutingIntegrationTests).Assembly],
            TypeFilter = type => type == typeof(ProtectedCatalogSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapQueryable();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartProxyAsync(WebApplication authority, WebApplication publisher, TaskCompletionSource<bool> read)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        });
        builder.Services.AddTestJwtBearer();
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryableCallerRoutingIntegrationTests).Assembly],
            TypeFilter = type => type == typeof(ForwardedCatalogSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddKaleidoClientHandler<ExchangeTokenHandler>()
            .AddRegistryHttpParticipation(new KaleidoClientOptions { OnBehalfOfCalls = KaleidoOnBehalfOfCalls.Exchange })
            .AddQueryableHttpClient();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveReadHandler(read)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-queryable", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = publisher.GetTestServer().CreateHandler()));
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapQueryable();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task WaitUntilReadyAsync(WebApplication proxy)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var health = proxy.Services.GetRequiredService<HealthCheckService>();
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var report = await health.CheckHealthAsync(check => check.Name == "kaleido-registration", timeout.Token);
            if (report.Status == HealthStatus.Healthy)
            {
                return;
            }

            await Task.Yield();
        }
    }

    [QuerySource(DisplayName = "Protected catalog", Description = "A source restricted to radiology")]
    [KaleidoAuthorization(Roles = "radiology")]
    public sealed class ProtectedCatalogSource : IQuerySource<ProtectedRecord>
    {
        public IQueryable<ProtectedRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new ProtectedRecord { Name = "available" } }.AsQueryable();
    }

    public sealed class ProtectedRecord : IQueryContext
    {
        public string Name { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Forwarded catalog", Description = "A caller-scoped remote Queryable request")]
    public sealed class ForwardedCatalogSource(IKaleidoQueryableClientFactory clients) : IQuerySourceAsync<ForwardedRecord>
    {
        public async Task<IQueryable<ForwardedRecord>> CreateQueryAsync(QueryExecutionContext executionContext, CancellationToken cancellationToken)
        {
            var result = await clients.GetClient("catalog").QuerySourceAsync<ProtectedRecord>("ProtectedCatalogSource", new QueryRequest(), cancellationToken);
            return result.Results.Select(x => new ForwardedRecord { Name = x.Name }).AsQueryable();
        }
    }

    public sealed class ForwardedRecord : IQueryContext
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ExchangeTokenHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);
            if (call.Purpose == KaleidoCallPurpose.ServiceOwn)
            {
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("reader-proxy", ["registry-reader"]));
            }
            else
            {
                var caller = call.Caller?.Identity?.Name ?? "anonymous";
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(caller,
                    call.Caller?.IsInRole("radiology") == true ? ["radiology"] : [], app: "reader-exchange", audience: CatalogAudience));
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ObserveReadHandler(TaskCompletionSource<bool> observed) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Get
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/capabilities", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                observed.TrySetResult(true);
            }

            return response;
        }
    }
}
