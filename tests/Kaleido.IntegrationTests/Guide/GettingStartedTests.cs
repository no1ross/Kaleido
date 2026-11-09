using System.Text.Json;
using Kaleido.Guide.Examples.GettingStarted;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/getting-started.md, run as written: the orders service from its compiled examples,
// called over HTTP the way the guide shows.
public sealed class GettingStartedTests : IAsyncLifetime
{
    private IHost? _host;
    private TestControlRegistry? _registry;

    private HttpClient Client => (_host ?? throw new InvalidOperationException("Not started.")).GetTestClient();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _registry = await TestControlRegistry.StartAsync();
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    OrdersService.AddOrders(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Kaleido:BaseUrl"] = "http://registry.test/",
                    }).Build());
                    services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => _registry!.CreateHandler());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(OrdersService.MapOrders);
                });
            })
            .StartAsync(Cancel);
        await _registry.WaitForRegistrationAsync(_host);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(Cancel);
            _host.Dispose();
        }

        if (_registry is not null)
        {
            await _registry.DisposeAsync();
        }
    }

    private async Task<JsonElement> PostAsync(string url, object body, Guid? processId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };

        if (processId is { } id)
        {
            request.Headers.Add("X-Kaleido-Process-Id", id.ToString());
        }

        using var response = await Client.SendAsync(request, Cancel);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
    }

    [Fact]
    public async Task PlacingAnOrder_StartsAProcess_ThatRequiresTheConfirmation_AndTheOrderCanBeQueried()
    {
        await PostAsync("/orders/1/processes/steps/PlaceOrderStep", new { processStep = new { item = "Pen" } });
        var placed = await PostAsync("/orders/1/processes/steps/PlaceOrderStep", new { processStep = new { item = "Book", quantity = 2 } });

        Assert.Equal("completed", placed.GetProperty("status").GetString());
        Assert.Equal(2, placed.GetProperty("result").GetProperty("number").GetInt32());
        Assert.Equal("ConfirmOrderStep", placed.GetProperty("requiredStep").GetProperty("name").GetString());

        var processId = placed.GetProperty("processId").GetGuid();
        var confirmed = await PostAsync("/orders/1/processes/steps/ConfirmOrderStep", new { processStep = new { } }, processId);

        Assert.Equal("completed", confirmed.GetProperty("status").GetString());

        var orders = await PostAsync("/orders/1/queryable/OrderSource/query", new { query = new { searchText = "Book" } });
        var order = Assert.Single(orders.GetProperty("results").EnumerateArray());
        Assert.Equal("Book", order.GetProperty("item").GetString());
        Assert.Equal(2, order.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheRegistry_PublishesTheStepsAndTheSource()
    {
        using var registryClient = _registry!.CreateClient();
        using var response = await registryClient.GetAsync("/registry", Cancel);
        var registry = await response.Content.ReadAsStringAsync(Cancel);

        Assert.True(response.IsSuccessStatusCode, registry);
        Assert.Contains("PlaceOrderStep", registry, StringComparison.Ordinal);
        Assert.Contains("ConfirmOrderStep", registry, StringComparison.Ordinal);
        Assert.Contains("OrderSource", registry, StringComparison.Ordinal);
    }
}
