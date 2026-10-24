using System.Text.Json;
using Kaleido.Http.Client;
using Kaleido.IntegrationTests.Guide.Returns;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/processes.md, run: relationships, outcomes and a handoff, as the page states them.
public sealed class ProcessesExampleTests
{
    private static readonly object Book = new { item = "Book" };

    private static IEnumerable<string> Available(JsonElement answer) =>
        answer.TryGetProperty("availableSteps", out var steps) && steps.ValueKind == JsonValueKind.Array
            ? steps.EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? string.Empty)
            : [];

    private static async Task<Guid> ConfirmedOrderAsync(GuideHost host)
    {
        var placed = await host.StepAsync("PlaceOrderStep", Book);
        var processId = placed.GetProperty("processId").GetGuid();
        await host.StepAsync("ConfirmOrderStep", new { }, processId);
        return processId;
    }

    [Fact]
    public async Task ARejectedStep_LeavesTheProcessAsItWas_AndCanBeCalledAgain()
    {
        await using var host = await GuideHost.StartAsync();
        var processId = await ConfirmedOrderAsync(host);

        var rejected = await host.StepAsync("ShipOrderStep", new { carrier = "courier", address = "1 Main St" }, processId);
        var shipped = await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "1 Main St" }, processId);

        Assert.Equal("rejected", rejected.GetProperty("outcome").GetString());
        Assert.Contains("ShipOrderStep", Available(rejected));
        Assert.Equal("completed", shipped.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task AFailedStep_EndsTheProcess()
    {
        await using var host = await GuideHost.StartAsync();
        var processId = await ConfirmedOrderAsync(host);

        var failed = await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "Base 1, Antarctica" }, processId);
        var (_, state) = await host.SendAsync(HttpMethod.Get, $"/orders/processes/{processId}");

        Assert.Equal("failed", failed.GetProperty("outcome").GetString());
        Assert.Equal("businessFailure", state.GetProperty("state").GetString());
    }

    // While a step is required, another step does not run: the answer is pending, with the required
    // step again (not an error).
    [Fact]
    public async Task WhileAStepIsRequired_AnotherStepIsAnsweredPending_WithTheRequiredStep()
    {
        await using var host = await GuideHost.StartAsync();
        var placed = await host.StepAsync("PlaceOrderStep", Book);

        var note = await host.StepAsync("AddNoteStep", new { text = "Leave at the door" }, placed.GetProperty("processId").GetGuid());

        Assert.Equal("pending", note.GetProperty("outcome").GetString());
        Assert.Equal("ConfirmOrderStep", note.GetProperty("requiredStep").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ARepeatableStep_RunsAgain_AndAStepWithoutRepeatable_IsNotOfferedAgain()
    {
        await using var host = await GuideHost.StartAsync();
        var processId = await ConfirmedOrderAsync(host);

        var first = await host.StepAsync("AddNoteStep", new { text = "Leave at the door" }, processId);
        var second = await host.StepAsync("AddNoteStep", new { text = "Ring twice" }, processId);

        Assert.Equal("completed", first.GetProperty("outcome").GetString());
        Assert.Equal("completed", second.GetProperty("outcome").GetString());
        Assert.Contains("AddNoteStep", Available(second));
        Assert.DoesNotContain("ConfirmOrderStep", Available(second));
    }

    [Fact]
    public async Task AvailableUntil_ClosesCancelOnceShipped_AndShippingOnceCancelled()
    {
        await using var host = await GuideHost.StartAsync();
        var shippedProcess = await ConfirmedOrderAsync(host);
        var cancelledProcess = await ConfirmedOrderAsync(host);

        var shipped = await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "1 Main St" }, shippedProcess);
        var cancelled = await host.StepAsync("CancelOrderStep", new { }, cancelledProcess);

        Assert.DoesNotContain("CancelOrderStep", Available(shipped));
        Assert.Contains("ReturnOrderStep", Available(shipped));
        Assert.DoesNotContain("ShipOrderStep", Available(cancelled));
    }

    // #450: the handoff as the page states it - the answer names the processor that continues, and the
    // handler has started the process there under the same id, so the caller can read it and run its
    // next step in the returns service.
    [Fact]
    public async Task AHandOff_NamesTheProcessorThatContinues_WhichHasTheProcessUnderTheSameId()
    {
        await using var returns = await ReturnsHost.StartAsync();
        await using var host = await GuideHost.StartAsync(
            configure: kaleido => kaleido.AddHttpClients(),
            services: s => s.AddHttpClient("returns").ConfigurePrimaryHttpMessageHandler(returns.CreateHandler),
            settings: new Dictionary<string, string?>
            {
                ["Kaleido:Clients:returns:BaseUrl"] = "http://returns.test/",
                ["Kaleido:Clients:returns:RoutePrefix"] = "returns",
            });
        var processId = await ConfirmedOrderAsync(host);
        await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "1 Main St" }, processId);

        var returned = await host.StepAsync("ReturnOrderStep", new { }, processId);

        Assert.Equal("completed", returned.GetProperty("outcome").GetString());
        Assert.Equal("returns", returned.GetProperty("targetProcessorName").GetString());

        var (stateStatus, state) = await returns.SendAsync(HttpMethod.Get, $"/returns/processes/{processId}", processId);
        Assert.Equal(System.Net.HttpStatusCode.OK, stateStatus);
        Assert.Contains(
            state.GetProperty("steps").EnumerateArray(),
            x => x.GetProperty("stepName").GetString() == nameof(Returns.OpenReturnStep) && x.GetProperty("status").GetString() == "completed");

        var (receivedStatus, received) = await returns.SendAsync(
            HttpMethod.Post, $"/returns/processes/steps/{nameof(Returns.ReceiveReturnStep)}", processId, new { processStep = new { } });
        Assert.Equal(System.Net.HttpStatusCode.OK, receivedStatus);
        Assert.Equal("completed", received.GetProperty("outcome").GetString());
        Assert.Equal(processId, received.GetProperty("processId").GetGuid());
    }
}
