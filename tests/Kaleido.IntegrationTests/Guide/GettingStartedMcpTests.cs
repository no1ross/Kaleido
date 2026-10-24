using System.Text.Json.Nodes;
using Kaleido.Guide.Examples.GettingStarted;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/getting-started.md, "Publish it to AI agents (MCP)", hosted as written (#450): the
// registration and mapping are the guide's own (OrdersService.AddOrdersWithMcp / MapOrdersWithMcp),
// with the configuration the page shows; an MCP client lists the tools and calls one.
public sealed class GettingStartedMcpTests : IAsyncLifetime
{
    private WebApplication? _app;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private WebApplication App => _app ?? throw new InvalidOperationException("Not started.");

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:Clients:orders:BaseUrl"] = "http://localhost/",
            ["Kaleido:Clients:orders:RoutePrefix"] = "orders",
        });

        OrdersService.AddOrdersWithMcp(builder.Services, builder.Configuration);

        // The orders client calls this server (in production: the network).
        builder.Services.AddHttpClient("orders")
            .ConfigurePrimaryHttpMessageHandler(sp => ((TestServer)sp.GetRequiredService<IServer>()).CreateHandler());

        _app = builder.Build();
        OrdersService.MapOrdersWithMcp(_app);
        await _app.StartAsync(Cancel);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync(Cancel);
            await _app.DisposeAsync();
        }
    }

    private async Task<McpClient> ConnectAsync() =>
        await McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
                App.GetTestClient()),
            cancellationToken: Cancel);

    // The page: one tool per capability, named orders_<capability>; after "Place order" the agent is
    // told that "Confirm order" is next.
    [Fact]
    public async Task AnAgent_SeesTheServicesTools_AndPlacingAnOrder_TellsItToConfirm()
    {
        await using var client = await ConnectAsync();

        var tools = (await client.ListToolsAsync(cancellationToken: Cancel)).Select(x => x.Name).ToArray();
        var placed = await client.CallToolAsync("orders_place_order", new Dictionary<string, object?> { ["item"] = "Book", ["quantity"] = 2 }, cancellationToken: Cancel);

        Assert.Contains("orders_place_order", tools);
        Assert.Contains("orders_confirm_order", tools);
        Assert.Contains(tools, x => x.StartsWith("orders_order", StringComparison.Ordinal));
        Assert.NotEqual(true, placed.IsError);
        var body = JsonNode.Parse(placed.Content.OfType<TextContentBlock>().First().Text);
        Assert.Equal("orders_confirm_order", body?["next"]?["required"]?["tool"]?.GetValue<string>());
    }
}
