using System.Text.Json.Nodes;
using Kaleido.Guide.Examples.Callers;
using Kaleido.Guide.Examples.GettingStarted;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Mcp.FunctionalTests.Guide;

public sealed class GettingStartedMcpGuideFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAgent_SeesThePublishedOrdersTools_AndPlacingAnOrderNamesTheConfirmation()
    {
        await using var registry = await TestControlRegistry.StartAsync();
        await using var publisher = await StartPublisherAsync(registry);
        await registry.WaitForRegistrationAsync(publisher);
        await using var reader = await StartReaderAsync(registry, publisher);
        await WaitUntilReadyAsync(reader);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice")).ToString(),
        };
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        }, reader.GetTestClient());
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: Cancel);

        var tools = (await client.ListToolsAsync(cancellationToken: Cancel)).Select(tool => tool.Name).ToArray();
        var placed = await client.CallToolAsync("orders_place_order", new Dictionary<string, object?>
        {
            ["item"] = "Book",
            ["quantity"] = 2,
        }, cancellationToken: Cancel);

        Assert.Contains("orders_place_order", tools);
        Assert.Contains("orders_confirm_order", tools);
        Assert.Contains(tools, name => name.StartsWith("orders_order", StringComparison.Ordinal));
        Assert.NotEqual(true, placed.IsError);
        var body = JsonNode.Parse(placed.Content.OfType<TextContentBlock>().First().Text);
        Assert.Equal("orders_confirm_order", body?["next"]?["required"]?["tool"]?.GetValue<string>());
    }

    private static async Task<WebApplication> StartPublisherAsync(TestControlRegistry registry)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        OrdersService.AddOrders(builder.Services, builder.Configuration);
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistryCredentialHandler("orders")
            {
                InnerHandler = registry.CreateHandler(),
            }));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        OrdersService.MapOrders(app);
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<WebApplication> StartReaderAsync(TestControlRegistry registry, WebApplication publisher)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddSingleton<ITokenExchange, SignedTokenExchange>();
        OrdersService.AddOrdersWithMcp(builder.Services, builder.Configuration);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = TestTokenIssuer.Issuer,
                ValidAudience = TestTokenIssuer.PlatformAudience,
                IssuerSigningKey = TestTokenIssuer.SigningKey,
                NameClaimType = TestTokenIssuer.NameClaimType,
                RoleClaimType = TestTokenIssuer.RoleClaimType,
                ClockSkew = TimeSpan.Zero,
            };
        });
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistryCredentialHandler("mcp-reader")
            {
                InnerHandler = registry.CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = publisher.GetTestServer().CreateHandler()));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-queryable", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = publisher.GetTestServer().CreateHandler()));
        var app = builder.Build();
        OrdersService.MapOrdersWithMcp(app);
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task WaitUntilReadyAsync(WebApplication reader)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var health = reader.Services.GetRequiredService<HealthCheckService>();
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

    private sealed class SignedTokenExchange : ITokenExchange
    {
        public Task<string> ExchangeAsync(string callerAuthorization, CancellationToken cancellationToken) =>
            Task.FromResult("Bearer " + TestTokenIssuer.User("alice", app: "guide-mcp-exchange", tokenId: Guid.NewGuid().ToString("N")));
    }

    private sealed class RegistryCredentialHandler(string service) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (KaleidoCallContext.Of(request).Purpose == KaleidoCallPurpose.ServiceOwn)
            {
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App(service + "-app",
                    ["registrant", "registry-reader", $"Kaleido.Registrant.{service}"]));
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
