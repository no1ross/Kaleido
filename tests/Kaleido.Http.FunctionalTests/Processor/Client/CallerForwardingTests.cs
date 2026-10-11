using System.Net;
using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Processor.Client;

/// <summary>
/// The real client pipeline, set up the way the PriorAuth router sets up its clients (a service
/// token on every call, plus a host handler that stamps its own credentials): inside a
/// ForwardCaller() scope the caller's token is what reaches the service; outside it, nothing changes.
/// </summary>
public sealed class CallerForwardingTests
{
    private const string ServiceToken = "Bearer svc-router";

    private sealed class HostCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-Host-Handler", "ran");
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer on-behalf-of-with-actor");
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ProcessStateResponse { ProcessId = Guid.Empty }, options: KaleidoJsonOptions.Options)
            });
        }
    }

    private static (ServiceProvider Provider, CaptureHandler Wire) Build(bool hostHandler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:ServiceName"] = "router",
                ["Kaleido:Clients:Intake:BaseUrl"] = "http://intake.test/",
                ["Kaleido:Clients:Intake:RoutePrefix"] = "intake"
            })
            .Build();

        var wire = new CaptureHandler();
        var services = new ServiceCollection();

        services.AddLogging();
        // No capabilities of its own: only the client pipeline is under test.
        services.AddKaleido(configuration, o =>
            {
                o.Assemblies = [typeof(CallerForwardingTests).Assembly];
                o.TypeFilter = _ => false;
            })
            .AddHttpClients();

        var client = services.AddHttpClient(
                "Intake",
                c => c.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", ServiceToken))
            .ConfigurePrimaryHttpMessageHandler(() => wire);

        if (hostHandler)
        {
            services.AddTransient<HostCredentialsHandler>();
            client.AddHttpMessageHandler<HostCredentialsHandler>();
        }

        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }), wire);
    }

    private static async Task<HttpRequestMessage> GetStateAsync(ServiceProvider provider, CaptureHandler wire, Func<IDisposable?> scope)
    {
        using var serviceScope = provider.CreateScope();
        var client = serviceScope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("Intake");

        using (scope())
        {
            await client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        }

        return wire.Sent[^1];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsideAScope_TheCallersTokenReachesTheService_WhateverTheHostConfigured(bool hostHandler)
    {
        var (provider, wire) = Build(hostHandler);
        using var _ = provider;
        var forwarding = provider.GetRequiredService<IKaleidoCallerForwarding>();

        var sent = await GetStateAsync(provider, wire, () => forwarding.ForwardCaller("Bearer carol-token"));

        Assert.Equal(["Bearer carol-token"], sent.Headers.GetValues("Authorization"));
        Assert.Equal(hostHandler, sent.Headers.Contains("X-Host-Handler"));
    }

    [Fact]
    public async Task InsideAScope_AnAnonymousCaller_ReachesTheServiceAnonymous_NotWithTheServiceToken()
    {
        var (provider, wire) = Build(hostHandler: true);
        using var _ = provider;
        var forwarding = provider.GetRequiredService<IKaleidoCallerForwarding>();

        var sent = await GetStateAsync(provider, wire, () => forwarding.ForwardCaller(null));

        Assert.False(sent.Headers.Contains("Authorization"));
    }

    [Theory]
    [InlineData(false, ServiceToken)]
    [InlineData(true, "Bearer on-behalf-of-with-actor")]
    public async Task OutsideAScope_TheHostsCredentialsAreSent(bool hostHandler, string expected)
    {
        var (provider, wire) = Build(hostHandler);
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, () => null);

        Assert.Equal([expected], sent.Headers.GetValues("Authorization"));
    }
}
