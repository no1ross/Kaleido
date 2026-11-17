using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Registry.IntegrationTests;

public sealed class RegistryCallerExchangeIntegrationTests
{
    [Fact]
    public async Task AuthenticatedQueryableCall_ExchangesTheCallerTokenForFilteredRegistryDiscovery()
    {
        await using var authority = await StartAuthorityAsync();
        await PublishAsync(authority);
        await using var proxy = await StartProxyAsync(authority);
        using var client = proxy.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");

        Assert.Equal(2, await VisibleCountAsync(client, "alice"));
        Assert.Equal(1, await VisibleCountAsync(client, "bob"));
    }

    private static async Task<int> VisibleCountAsync(HttpClient client, string caller)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/reader/1/queryable/callerlookupsource/query")
        {
            Content = JsonContent.Create(new { query = new { } }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(caller));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        return body.GetProperty("results")[0].GetProperty("visibleCount").GetInt32();
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
                Queryables =
                [
                    new QueryableRegistryItem { ServiceName = "catalog", Name = "public", Authorization = new AuthorizationMetadata(null, []) { AllowAnonymous = true } },
                    new QueryableRegistryItem { ServiceName = "catalog", Name = "protected", Authorization = new AuthorizationMetadata(null, ["radiology"]) },
                ],
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
            Assemblies = [typeof(RegistryCallerExchangeIntegrationTests).Assembly],
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

    private static async Task<WebApplication> StartProxyAsync(WebApplication authority)
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
            Assemblies = [typeof(RegistryCallerExchangeIntegrationTests).Assembly],
            TypeFilter = type => type == typeof(CallerLookupSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddKaleidoClientHandler<ExchangeTokenHandler>()
            .AddRegistryHttpClient(new KaleidoClientOptions { OnBehalfOfCalls = KaleidoOnBehalfOfCalls.Exchange });
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = authority.GetTestServer().CreateHandler()));
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapQueryable();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    [QuerySource(DisplayName = "Caller lookup", Description = "The caller's visible Registry sources")]
    public sealed class CallerLookupSource(IKaleidoRegistryClient registry) : IQuerySourceAsync<CallerLookupRow>
    {
        public async Task<IQueryable<CallerLookupRow>> CreateQueryAsync(QueryExecutionContext executionContext, CancellationToken cancellationToken)
        {
            var visible = await registry.GetRegistryAsync(null, cancellationToken);
            return new[] { new CallerLookupRow { VisibleCount = visible.Value?.Queryables.Count ?? -1 } }.AsQueryable();
        }
    }

    public sealed class CallerLookupRow : IQueryContext
    {
        public int VisibleCount { get; init; }
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
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(caller, caller == "alice" ? ["radiology"] : [], app: "registry-exchange"));
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
