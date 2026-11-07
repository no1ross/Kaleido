using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Kaleido.Client.Processor;
using Kaleido.Guide.Examples.Callers;
using Kaleido.Observability;
using Kaleido.Platform;
using Kaleido.Platform.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    // "Which token a service sends", as the page registers it (CallCredentialsRegistration): with
    // Kaleido:OnBehalfOfCalls = Exchange, a call made for a caller carries the token the example's handler
    // exchanged; the service's own call is left to the host's other handlers (#574).
    [Fact]
    public async Task TheTokenExchangeHandler_ExchangesACallersToken_AndLeavesTheServicesOwnCall()
    {
        var wire = new CaptureHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITokenExchange, PrefixingTokenExchange>();
        services.AddSingleton<Kaleido.Guide.Examples.GettingStarted.OrderBook>();
        CallCredentialsRegistration.AddWithTokenExchange(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:OnBehalfOfCalls"] = "Exchange",
            ["Kaleido:Clients:member:BaseUrl"] = "https://member.test/",
        }).Build());
        services.AddHttpClient("member").ConfigurePrimaryHttpMessageHandler(() => wire);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using (var request = provider.CreateScope())
        {
            request.ServiceProvider.GetRequiredService<IKaleidoInboundCallerAccessor>()
                .Set(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann")], "test")), "Bearer ann-inbound");
            await request.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("member")
                .GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        }

        using (var own = provider.CreateScope())
        {
            await own.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("member")
                .GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        }

        Assert.Equal(["Bearer exchanged:Bearer ann-inbound"], wire.Sent[0].Headers.GetValues("Authorization"));
        Assert.False(wire.Sent[1].Headers.Contains("Authorization"));
    }

    // The identity provider's exchange the example assumes: a recognizable token per inbound one.
    private sealed class PrefixingTokenExchange : ITokenExchange
    {
        public Task<string> ExchangeAsync(string callerAuthorization, CancellationToken cancellationToken) =>
            Task.FromResult($"Bearer exchanged:{callerAuthorization}");
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ProcessStateResponse { ProcessId = Guid.Empty }, options: KaleidoJsonOptions.Options),
            });
        }
    }
}
