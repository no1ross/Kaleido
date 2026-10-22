using System.Net.Http.Json;
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

    private HttpClient Client => (_host ?? throw new InvalidOperationException("Not started.")).GetTestClient();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    OrdersService.AddOrders(services, new ConfigurationBuilder().Build());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(OrdersService.MapOrders);
                });
            })
            .StartAsync(Cancel);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(Cancel);
            _host.Dispose();
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
        await PostAsync("/orders/processes/steps/PlaceOrderStep", new { processStep = new { item = "Pen" } });
        var placed = await PostAsync("/orders/processes/steps/PlaceOrderStep", new { processStep = new { item = "Book", quantity = 2 } });

        Assert.Equal("completed", placed.GetProperty("outcome").GetString());
        Assert.Equal(2, placed.GetProperty("result").GetProperty("number").GetInt32());
        Assert.Equal("ConfirmOrderStep", placed.GetProperty("requiredStep").GetProperty("name").GetString());

        var processId = placed.GetProperty("processId").GetGuid();
        var confirmed = await PostAsync("/orders/processes/steps/ConfirmOrderStep", new { processStep = new { } }, processId);

        Assert.Equal("completed", confirmed.GetProperty("outcome").GetString());

        var orders = await PostAsync("/orders/queryable/OrderSource/query", new { query = new { searchText = "Book" } });
        var order = Assert.Single(orders.GetProperty("results").EnumerateArray());
        Assert.Equal("Book", order.GetProperty("item").GetString());
        Assert.Equal(2, order.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheRegistry_PublishesTheStepsAndTheSource()
    {
        using var response = await Client.GetAsync("/orders/registry", Cancel);
        var registry = await response.Content.ReadAsStringAsync(Cancel);

        Assert.True(response.IsSuccessStatusCode, registry);
        Assert.Contains("PlaceOrderStep", registry, StringComparison.Ordinal);
        Assert.Contains("ConfirmOrderStep", registry, StringComparison.Ordinal);
        Assert.Contains("OrderSource", registry, StringComparison.Ordinal);
    }
}
