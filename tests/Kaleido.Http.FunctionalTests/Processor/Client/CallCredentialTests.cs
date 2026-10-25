using System.Net;
using System.Security.Claims;
using Kaleido.Exceptions;
using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Processor.Client;

/// <summary>
/// The real client pipeline with an <see cref="IKaleidoCallCredentialProvider"/> (ADR 0022), set up the
/// way the PriorAuth hosts set up their clients (a service token on every call, plus a host handler that
/// stamps its own credentials): a call made while handling a caller's request sends what the provider
/// decides for that caller - never the host's credentials -, and the service's own calls (registry
/// fetches, calls outside a request) are the provider's to authorize as the service.
/// </summary>
public sealed class CallCredentialTests
{
    private const string ServiceToken = "Bearer svc-router";

    private static readonly ClaimsPrincipal Carol = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "carol")], "test"));

    private sealed class HostCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-Host-Handler", "ran");
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer host-handler-token");
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Sent)
            {
                Sent.Add(request);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ProcessStateResponse { ProcessId = Guid.Empty }, options: KaleidoJsonOptions.Options)
            });
        }
    }

    // A token exchange stand-in: a token for the caller addressed to the destination, a service token for own calls.
    private sealed class ExchangeProvider : IKaleidoCallCredentialProvider
    {
        public List<KaleidoCallCredentialRequest> Asked { get; } = [];

        public ValueTask<KaleidoCallCredential> GetCredentialAsync(KaleidoCallCredentialRequest request, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add(request);
            }

            return ValueTask.FromResult(KaleidoCallCredential.Header(
                request.Purpose == KaleidoCallPurpose.OnBehalfOfCaller
                    ? $"Bearer {request.Caller?.Identity?.Name}-for-{request.ServiceName}"
                    : $"Bearer svc-for-{request.ServiceName}"));
        }
    }

    private sealed class ConfiguredForEveryoneProvider : IKaleidoCallCredentialProvider
    {
        public ValueTask<KaleidoCallCredential> GetCredentialAsync(KaleidoCallCredentialRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(KaleidoCallCredential.Configured);
    }

    private static (ServiceProvider Provider, CaptureHandler Wire) Build(Action<IKaleidoBuilder> credentials, bool hostHandler = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:ServiceName"] = "router",
                ["Kaleido:Clients:Intake:BaseUrl"] = "http://intake.test/",
                ["Kaleido:Clients:Intake:RoutePrefix"] = "intake",
                ["Kaleido:Clients:Member:BaseUrl"] = "http://member.test/",
                ["Kaleido:Clients:Member:RoutePrefix"] = "member",
            })
            .Build();

        var wire = new CaptureHandler();
        var services = new ServiceCollection();

        services.AddLogging();
        // No capabilities of its own: only the client pipeline is under test.
        var kaleido = services.AddKaleido(configuration, o =>
            {
                o.Assemblies = [typeof(CallCredentialTests).Assembly];
                o.TypeFilter = _ => false;
            })
            .AddInMemoryStores()
            .AddHttpClients();
        credentials(kaleido);

        foreach (var name in new[] { "Intake", "Member" })
        {
            var client = services.AddHttpClient(
                    name,
                    c => c.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", ServiceToken))
                .ConfigurePrimaryHttpMessageHandler(() => wire);

            if (hostHandler)
            {
                services.AddTransient<HostCredentialsHandler>();
                client.AddHttpMessageHandler<HostCredentialsHandler>();
            }
        }

        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }), wire);
    }

    // One request scope: `caller` set as the transport sets it (an inbound request), or none (the service's own call).
    private static async Task<HttpRequestMessage> GetStateAsync(ServiceProvider provider, CaptureHandler wire, string client, ClaimsPrincipal? caller, bool inbound = true)
    {
        using var scope = provider.CreateScope();

        if (inbound)
        {
            scope.ServiceProvider.GetRequiredService<IKaleidoInboundCallerAccessor>()
                .Set(caller ?? new ClaimsPrincipal(new ClaimsIdentity()), caller is null ? null : "Bearer carol-inbound");
        }

        await scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient(client)
            .GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        return wire.Sent[^1];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForwardCallerToken_ACallersCall_SendsTheCallersToken_WhateverTheHostConfigured(bool hostHandler)
    {
        var (provider, wire) = Build(k => k.ForwardCallerToken(), hostHandler);
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", Carol);

        Assert.Equal(["Bearer carol-inbound"], sent.Headers.GetValues("Authorization"));
        Assert.Equal(hostHandler, sent.Headers.Contains("X-Host-Handler"));
    }

    [Fact]
    public async Task ForwardCallerToken_TheServicesOwnCall_KeepsTheHostsCredentials()
    {
        var (provider, wire) = Build(k => k.ForwardCallerToken());
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", caller: null, inbound: false);

        Assert.Equal(["Bearer host-handler-token"], sent.Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task AnAnonymousCaller_ReachesTheServiceAnonymous_NeverWithTheHostsCredentials()
    {
        var exchange = new ExchangeProvider();
        var (provider, wire) = Build(k => k.Services.AddSingleton<IKaleidoCallCredentialProvider>(exchange));
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", caller: null);

        Assert.False(sent.Headers.Contains("Authorization"));
        Assert.Empty(exchange.Asked);
    }

    // The token a host exchanges per destination is what is sent: no longer overwritten (#287).
    [Fact]
    public async Task AnExchangeProvider_EachDestinationGetsItsOwnToken_ForTheCaller()
    {
        var exchange = new ExchangeProvider();
        var (provider, wire) = Build(k => k.Services.AddSingleton<IKaleidoCallCredentialProvider>(exchange));
        using var _ = provider;

        var toIntake = await GetStateAsync(provider, wire, "Intake", Carol);
        var toMember = await GetStateAsync(provider, wire, "Member", Carol);

        Assert.Equal(["Bearer carol-for-intake"], toIntake.Headers.GetValues("Authorization"));
        Assert.Equal(["Bearer carol-for-member"], toMember.Headers.GetValues("Authorization"));
        Assert.All(exchange.Asked, x => Assert.Equal("Bearer carol-inbound", x.CallerAuthorization));
    }

    [Fact]
    public async Task ARegistryFetch_InsideACallersRequest_IsTheServicesOwnCall()
    {
        var exchange = new ExchangeProvider();
        var (provider, wire) = Build(k => k.Services.AddSingleton<IKaleidoCallCredentialProvider>(exchange));
        using var _ = provider;
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IKaleidoInboundCallerAccessor>().Set(Carol, "Bearer carol-inbound");

        await scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("Member")
            .GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Bearer svc-for-member"], wire.Sent[^1].Headers.GetValues("Authorization"));
        Assert.Equal(KaleidoCallPurpose.ServiceOwn, exchange.Asked[^1].Purpose);
    }

    [Fact]
    public async Task AProviderThatKeepsTheHostsCredentialsForACaller_IsRefused()
    {
        var (provider, wire) = Build(k => k.AddCallCredentials<ConfiguredForEveryoneProvider>());
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => GetStateAsync(provider, wire, "Intake", Carol));

        Assert.Equal(HttpClientErrorCodes.ConfiguredCredentialForCaller, exception.Code);
        Assert.Empty(wire.Sent);
    }

    [Fact]
    public async Task ConcurrentCallers_EachSendTheirOwnCredential()
    {
        var exchange = new ExchangeProvider();
        var (provider, wire) = Build(k => k.Services.AddSingleton<IKaleidoCallCredentialProvider>(exchange));
        using var _ = provider;
        var callers = Enumerable.Range(0, 16).Select(i => new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, $"user{i}")], "test"))).ToArray();

        await Task.WhenAll(callers.Select(caller => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IKaleidoInboundCallerAccessor>().Set(caller, $"Bearer {caller.Identity?.Name}-inbound");
            await scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("Intake")
                .GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        })));

        var sent = wire.Sent.Select(x => x.Headers.GetValues("Authorization").Single()).Order(StringComparer.Ordinal);
        Assert.Equal(callers.Select(x => $"Bearer {x.Identity?.Name}-for-intake").Order(StringComparer.Ordinal), sent);
    }

    // Without a provider (a host without MCP that never registered one) the host's handlers decide, as before.
    [Fact]
    public async Task WithoutAProvider_TheHostsCredentialsAreSent_EvenForACaller()
    {
        var (provider, wire) = Build(_ => { });
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", Carol);

        Assert.Equal(["Bearer host-handler-token"], sent.Headers.GetValues("Authorization"));
    }
}
