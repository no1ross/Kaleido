using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Guide.Examples.GettingStarted;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Hosting.Http.FunctionalTests.Guide;

// docs/guide/getting-started.md, run as written: the orders service from its compiled examples,
// called over HTTP the way the guide shows.
public sealed class GettingStartedTests : IAsyncLifetime
{
    private WebApplication? _host;
    private WebApplication? _registry;
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _registry = await StartRegistryAsync();
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "http://localhost/" });
        OrdersService.AddOrders(builder.Services, builder.Configuration);
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = _registry.GetTestServer().CreateHandler(),
            }));
        _host = builder.Build();
        OrdersService.MapOrders(_host);
        await _host.StartAsync(Cancel);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            await _host.DisposeAsync();
        }

        if (_registry is not null)
        {
            await _registry.StopAsync(CancellationToken.None);
            await _registry.DisposeAsync();
        }
    }

    [Fact]
    public async Task PlacingAnOrder_StartsAProcess_ThatRequiresTheConfirmation_AndTheOrderCanBeQueried()
    {
        using var client = _host!.GetTestClient();
        using var first = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item = "Pen" } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var placedResponse = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item = "Book", quantity = 2 } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, placedResponse.StatusCode);
        var placed = await placedResponse.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        var step = Assert.Single(placed.GetProperty("steps").EnumerateArray());
        Assert.Equal("completed", step.GetProperty("executionStatus").GetString());
        Assert.Equal(2, step.GetProperty("response").GetProperty("number").GetInt32());
        Assert.Equal("ConfirmOrderStep", placed.GetProperty("requiredStep").GetString());

        var processId = placed.GetProperty("processId").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders/1/processes/steps/confirmorderstep")
        {
            Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        using var confirmation = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var confirmed = await confirmation.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        Assert.Equal("completed", Assert.Single(confirmed.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());

        using var query = await client.PostAsJsonAsync("/orders/1/queryable/ordersource/query", new { query = new { searchText = "Book" } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        var orders = await query.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        var order = Assert.Single(orders.GetProperty("results").EnumerateArray());
        Assert.Equal("Book", order.GetProperty("item").GetString());
        Assert.Equal(2, order.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheRegistry_PublishesTheStepsAndTheSource()
    {
        using var client = _registry!.GetTestClient();
        using var response = await client.GetAsync("/registry", Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PlaceOrderStep", body, StringComparison.Ordinal);
        Assert.Contains("ConfirmOrderStep", body, StringComparison.Ordinal);
        Assert.Contains("OrderSource", body, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> StartRegistryAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(GettingStartedTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry()
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.MapKaleido().MapCentralRegistry();
        await app.StartAsync(Cancel);
        return app;
    }

    private sealed class RegistrationHandler(TaskCompletionSource registered) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/registry/instances" && response.IsSuccessStatusCode)
            {
                registered.TrySetResult();
            }

            return response;
        }
    }
}
