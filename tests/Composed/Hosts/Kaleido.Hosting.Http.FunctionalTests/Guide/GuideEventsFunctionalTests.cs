using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Guide.Examples.Events;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.Processes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Hosting.Http.FunctionalTests.Guide;

public sealed class GuideEventsFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AProcess_PublishesItsEvents_WithTheClassifiedAddressRedacted()
    {
        var feed = new CapturingFeed(events => events.Count(entry => entry.EventType == "process.step-completed.v1") >= 3
            && events.Any(entry => entry.EventType == "process.execution-completed.v1"));
        await using var host = await StartAsync(feed);
        using var client = host.GetTestClient();
        var placed = await StepAsync(client, "placeorderstep", new { item = "Book" }, null);
        var processId = placed.ProcessId;
        await StepAsync(client, "confirmorderstep", new { }, processId);
        await StepAsync(client, "shiporderstep", new { carrier = "post", address = "42 Secret Lane" }, processId);

        var sent = await feed.Completed.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var types = sent.Select(entry => entry.EventType).ToArray();

        Assert.Contains("process.created.v1", types);
        Assert.Equal(3, types.Count(type => type == "process.step-completed.v1"));
        Assert.Contains("process.execution-completed.v1", types);
        Assert.Contains(sent, entry => entry.EventType == "process.step-completed.v1" && entry.Json.Contains("ShipOrderStep", StringComparison.Ordinal));
        Assert.DoesNotContain(sent, entry => entry.Json.Contains("Secret Lane", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AQuery_PublishesQueryExecuted()
    {
        var feed = new CapturingFeed(events => events.Any(entry => entry.EventType == "query.executed.v1"));
        await using var host = await StartAsync(feed);
        using var client = host.GetTestClient();

        using var response = await client.PostAsJsonAsync("/orders/1/queryable/ordersource/query", new { query = new { } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sent = await feed.Completed.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        Assert.Contains(sent, entry => entry.EventType == "query.executed.v1");
    }

    private static async Task<ProcessResult> StepAsync(HttpClient client, string name, object step, Guid? processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/orders/1/processes/steps/{name}")
        {
            Content = JsonContent.Create(new { processStep = step }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        using var response = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
    }

    private static async Task<WebApplication> StartAsync(CapturingFeed feed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<OrderBook>();
        builder.Services.AddSingleton<IWarehouseFeed>(feed);
        var kaleido = builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
            TypeFilter = type => type != typeof(ReturnOrderStep) && type != typeof(ReturnOrderHandler),
        });
        ShopEvents.AddShopEvents(kaleido);
        kaleido.AddProcess()
            .AddQueryable()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer();
        var host = builder.Build();
        OrdersService.MapOrders(host);
        await host.StartAsync(Cancel);
        return host;
    }

    private sealed class CapturingFeed(Func<IReadOnlyList<(string EventType, string Json)>, bool> done) : IWarehouseFeed
    {
        private readonly ConcurrentQueue<(string EventType, string Json)> sent = new();
        private readonly TaskCompletionSource<IReadOnlyList<(string EventType, string Json)>> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<(string EventType, string Json)>> Completed => completed.Task;

        public Task SendAsync(string eventType, string json, CancellationToken cancellationToken)
        {
            sent.Enqueue((eventType, json));
            var snapshot = sent.ToArray();
            if (done(snapshot))
            {
                completed.TrySetResult(snapshot);
            }

            return Task.CompletedTask;
        }
    }
}
