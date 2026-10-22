using System.Collections.Concurrent;
using Kaleido.Guide.Examples.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/events-and-outbox.md, run: the events a process writes reach the publisher through
// Kaleido's outbox and relay, and a classified field is redacted before it is stored.
public sealed class EventsExampleTests
{
    private sealed class CapturingFeed : IWarehouseFeed
    {
        public ConcurrentQueue<(string EventType, string Json)> Sent { get; } = new();

        public Task SendAsync(string eventType, string json, CancellationToken cancellationToken)
        {
            Sent.Enqueue((eventType, json));
            return Task.CompletedTask;
        }
    }

    private static async Task<List<(string EventType, string Json)>> WaitForAsync(CapturingFeed feed, Func<List<(string EventType, string Json)>, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (!done([.. feed.Sent]) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return [.. feed.Sent];
    }

    [Fact]
    public async Task AProcess_PublishesItsEvents_WithTheClassifiedAddressRedacted()
    {
        var feed = new CapturingFeed();
        await using var host = await GuideHost.StartAsync(
            configure: ShopEvents.AddShopEvents,
            services: s => s.AddSingleton<IWarehouseFeed>(feed));

        var placed = await host.StepAsync("PlaceOrderStep", new { item = "Book" });
        var processId = placed.GetProperty("processId").GetGuid();
        await host.StepAsync("ConfirmOrderStep", new { }, processId);
        await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "42 Secret Lane" }, processId);

        var sent = await WaitForAsync(feed, x => x.Count(e => e.EventType == "process.step-completed.v1") >= 3);
        var types = sent.Select(x => x.EventType).ToList();

        Assert.Contains("process.created.v1", types);
        Assert.Equal(3, types.Count(x => x == "process.step-completed.v1"));
        Assert.Contains("process.execution-completed.v1", types);
        Assert.Contains(sent, x => x.EventType == "process.step-completed.v1" && x.Json.Contains("ShipOrderStep", StringComparison.Ordinal));
        Assert.DoesNotContain(sent, x => x.Json.Contains("Secret Lane", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AQuery_PublishesQueryExecuted()
    {
        var feed = new CapturingFeed();
        await using var host = await GuideHost.StartAsync(
            configure: ShopEvents.AddShopEvents,
            services: s => s.AddSingleton<IWarehouseFeed>(feed));

        await host.QueryAsync("OrderSource", new { });

        Assert.Contains(await WaitForAsync(feed, x => x.Count > 0), x => x.EventType == "query.executed.v1");
    }
}
