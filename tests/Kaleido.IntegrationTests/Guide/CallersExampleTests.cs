using System.Net;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/callers-and-correlation.md, run: what each caller sees and may call under the default
// mode (Authenticated), with one capability narrowed to a role.
public sealed class CallersExampleTests
{
    private static async Task<string> RegistryAsync(GuideHost host, string? caller)
    {
        var (status, body) = await host.SendAsync(HttpMethod.Get, "/orders/registry", caller: caller);
        Assert.Equal(HttpStatusCode.OK, status);
        return body.ToString();
    }

    private static async Task<Guid> ShippedOrderAsync(GuideHost host, string caller)
    {
        var placed = await host.StepAsync("PlaceOrderStep", new { item = "Book" }, caller: caller);
        var processId = placed.GetProperty("processId").GetGuid();
        await host.StepAsync("ConfirmOrderStep", new { }, processId, caller);
        await host.StepAsync("ShipOrderStep", new { carrier = "post", address = "1 Main St" }, processId, caller);
        return processId;
    }

    [Fact]
    public async Task TheRegistry_ShowsEachCallerOnlyWhatItMayCall()
    {
        await using var host = await GuideHost.StartAsync(KaleidoAuthorizationMode.Authenticated);

        var clerk = await RegistryAsync(host, "cleo|clerk");
        var manager = await RegistryAsync(host, "max|manager");

        Assert.Contains("ShipOrderStep", clerk, StringComparison.Ordinal);
        Assert.DoesNotContain("RefundOrderStep", clerk, StringComparison.Ordinal);
        Assert.Contains("RefundOrderStep", manager, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACallerWithoutTheRole_IsForbidden_AndAnAnonymousCaller_IsUnauthorized()
    {
        await using var host = await GuideHost.StartAsync(KaleidoAuthorizationMode.Authenticated);
        var processId = await ShippedOrderAsync(host, "cleo|clerk");

        var (clerk, _) = await host.SendAsync(HttpMethod.Post, "/orders/processes/steps/RefundOrderStep", new { processStep = new { } }, processId, "cleo|clerk");
        var (anonymous, _) = await host.SendAsync(HttpMethod.Post, "/orders/processes/steps/PlaceOrderStep", new { processStep = new { item = "Book" } });
        var refunded = await host.StepAsync("RefundOrderStep", new { }, processId, "max|manager");

        Assert.Equal(HttpStatusCode.Forbidden, clerk);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous);
        Assert.Equal("completed", refunded.GetProperty("status").GetString());
    }

    // The process id is not a permission: another signed-in caller holding it may continue it.
    [Fact]
    public async Task AnotherCallerHoldingTheProcessId_MayContinueTheProcess()
    {
        await using var host = await GuideHost.StartAsync(KaleidoAuthorizationMode.Authenticated);
        var placed = await host.StepAsync("PlaceOrderStep", new { item = "Book" }, caller: "ann");

        var confirmed = await host.StepAsync("ConfirmOrderStep", new { }, placed.GetProperty("processId").GetGuid(), "ben");

        Assert.Equal("completed", confirmed.GetProperty("status").GetString());
    }
}
