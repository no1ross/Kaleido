using System.Net;
using Kaleido.Platform.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Client;

// The resilience pipeline AddHttpClients() wires once on the shared gateway client, for every service.
public sealed class ClientResilienceTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static (IHttpClientFactory Factory, CountingHandler Handler, ServiceProvider Provider) Build(Dictionary<string, string?> settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://gateway.test/",
            ["Kaleido:Resilience:MaxRetryAttempts"] = "1",
            ["Kaleido:Resilience:RetryDelay"] = "00:00:00.001",
        };
        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IKaleidoRegistrySource>(Mock.Of<IKaleidoRegistrySource>());
        services.AddKaleido(configuration, new()
        {
            ServiceName = "caller",
            AuthorizationMode = KaleidoAuthorizationMode.None,
            Assemblies = [typeof(KaleidoServiceOptions).Assembly],
        })
            .AddInMemoryStores()
            .AddHttpClients();
        services.AddHttpClient(KaleidoClientTransport.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return (provider.GetRequiredService<IHttpClientFactory>(), handler, provider);
    }

    [Fact]
    public async Task AReadThatFailsTransiently_IsRetriedByOneGatewayPipeline()
    {
        var (factory, handler, provider) = Build([]);
        using var _ = provider;

        using var response = await factory.CreateClient(KaleidoClientTransport.ClientName).GetAsync("/member/1/processes/00000000-0000-0000-0000-000000000000", Cancel);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task TurningResilienceOffOnTheGateway_MakesOneAttempt()
    {
        var (factory, handler, provider) = Build(new() { ["Kaleido:Resilience:Enabled"] = "false" });
        using var _ = provider;

        using var response = await factory.CreateClient(KaleidoClientTransport.ClientName).GetAsync("/legacy/1/processes/00000000-0000-0000-0000-000000000000", Cancel);

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task OneSharedResilienceSetting_AppliesAcrossServices()
    {
        var (factory, handler, provider) = Build(new() { ["Kaleido:Resilience:MaxRetryAttempts"] = "3" });
        using var _ = provider;
        var gateway = factory.CreateClient(KaleidoClientTransport.ClientName);

        using var first = await gateway.GetAsync("/legacy/1/processes/00000000-0000-0000-0000-000000000000", Cancel);
        var firstAttempts = handler.Attempts;
        using var second = await gateway.GetAsync("/member/1/processes/00000000-0000-0000-0000-000000000000", Cancel);

        Assert.Equal(4, firstAttempts);
        Assert.Equal(4, handler.Attempts - firstAttempts);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _attempts;
        public int Attempts => _attempts;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
