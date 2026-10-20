using System.Net;
using Kaleido.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Client;

// The resilience pipeline as AddHttpClients() wires it, through real DI and IHttpClientFactory: once per
// named client (the processor and the queryable client share the name), shared settings with per-client
// overrides, and off where a client turns it off.
public sealed class ClientResilienceTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static (IHttpClientFactory Factory, CountingHandler Handler, ServiceProvider Provider) Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(settings)
            {
                ["Kaleido:Resilience:MaxRetryAttempts"] = "1",
                ["Kaleido:Resilience:RetryDelay"] = "00:00:00.001",
                ["Kaleido:Clients:member:BaseUrl"] = "http://member/",
                ["Kaleido:Clients:legacy:BaseUrl"] = "http://legacy/",
            })
            .Build();

        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddKaleido(configuration, o =>
            {
                o.ServiceName = "caller";
                o.AuthorizationMode = KaleidoAuthorizationMode.None;
                o.Assemblies = [typeof(KaleidoServiceOptions).Assembly];
            })
            .AddHttpClients();
        services.AddHttpClient("member").ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient("legacy").ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return (provider.GetRequiredService<IHttpClientFactory>(), handler, provider);
    }

    [Fact]
    public async Task AReadThatFailsTransiently_IsRetriedByOnePipeline()
    {
        var (factory, handler, provider) = Build([]);
        using var _ = provider;

        using var response = await factory.CreateClient("member").GetAsync("/member/registry", Cancel);

        // One attempt plus the one configured retry - not four, which two stacked pipelines would make.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task AClientThatTurnsResilienceOff_IsCalledOnce()
    {
        var (factory, handler, provider) = Build(new() { ["Kaleido:Clients:legacy:Resilience:Enabled"] = "false" });
        using var _ = provider;

        using var response = await factory.CreateClient("legacy").GetAsync("/legacy/registry", Cancel);

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task AClientOverride_AppliesToThatClientOnly()
    {
        var (factory, handler, provider) = Build(new() { ["Kaleido:Clients:legacy:Resilience:MaxRetryAttempts"] = "3" });
        using var _ = provider;

        using var legacy = await factory.CreateClient("legacy").GetAsync("/legacy/registry", Cancel);
        var legacyAttempts = handler.Attempts;
        using var member = await factory.CreateClient("member").GetAsync("/member/registry", Cancel);

        Assert.Equal(4, legacyAttempts);
        Assert.Equal(2, handler.Attempts - legacyAttempts);
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
