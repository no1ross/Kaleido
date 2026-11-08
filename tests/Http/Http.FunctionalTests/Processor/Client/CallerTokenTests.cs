using System.Net;
using System.Security.Claims;
using Kaleido.Client.Processor;
using Kaleido.Client.Registry;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Platform;
using Kaleido.Platform.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Processor.Client;

/// <summary>
/// The real client pipeline and the caller-token modes (#574). A call made while handling a caller's
/// request carries the caller's token unchanged (<c>Forward</c>, the default) or the token the host's
/// own handler set for it (<c>Exchange</c>, never the inbound one); an anonymous caller is sent nothing;
/// the service's own calls (registry fetches, calls outside a request) keep what the host's handler set.
/// </summary>
public sealed class CallerTokenTests
{
    private const string InboundToken = "Bearer carol-inbound";
    private const string ServiceToken = "Bearer svc-router";

    private static readonly ClaimsPrincipal Carol = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "carol")], "test"));

    // The host's own handler for the service's own calls, as a client-credentials library adds one.
    private sealed class ServiceTokenHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (KaleidoCallContext.Of(request).Purpose == KaleidoCallPurpose.ServiceOwn)
            {
                request.Headers.Remove("Authorization");
                request.Headers.TryAddWithoutValidation("Authorization", ServiceToken);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // A token exchange stand-in: for a caller's call, a token for that caller and the destination.
    private sealed class ExchangeHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);

            if (call.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && call.CallerAuthorization is not null)
            {
                request.Headers.Remove("Authorization");
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {call.Caller?.Identity?.Name}-for-{request.RequestUri?.Host}");
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // A handler that "exchanges" by sending the inbound token as it is: what Exchange mode must refuse.
    private sealed class PassThroughHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (KaleidoCallContext.Of(request).CallerAuthorization is { } inbound)
            {
                request.Headers.TryAddWithoutValidation("Authorization", inbound);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // A host handler added the plain HttpClient way, stamping its own credential on every call.
    private sealed class HostCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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

    private static (ServiceProvider Provider, CaptureHandler Wire) Build(
        Action<IKaleidoBuilder> handlers,
        string? onBehalfOfCalls = null,
        bool hostCredentialsHandler = false,
        bool registryClient = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Kaleido:ServiceName"] = "router",
            ["Kaleido:BaseUrl"] = "https://registry.test/",
            ["Kaleido:Clients:Intake:BaseUrl"] = "https://intake.test/",
            ["Kaleido:Clients:Intake:RoutePrefix"] = "intake",
            ["Kaleido:Clients:Member:BaseUrl"] = "https://member.test/",
            ["Kaleido:Clients:Member:RoutePrefix"] = "member",
        };

        if (onBehalfOfCalls is not null)
        {
            settings["Kaleido:OnBehalfOfCalls"] = onBehalfOfCalls;
        }

        var wire = new CaptureHandler();
        var services = new ServiceCollection();

        services.AddLogging();
        // No capabilities of its own: only the client pipeline is under test.
        var kaleido = services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new() { ServiceVersion = "1", Assemblies = [typeof(CallerTokenTests).Assembly], TypeFilter = _ => false })
            .AddInMemoryStores()
            .AddHttpClients();
        handlers(kaleido);
        if (registryClient)
        {
            kaleido.AddControlRegistryClient();
        }

        foreach (var name in registryClient ? new[] { "Intake", "Member", "kaleido-registry" } : ["Intake", "Member"])
        {
            var client = services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => wire);

            if (hostCredentialsHandler)
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
                .Set(caller ?? new ClaimsPrincipal(new ClaimsIdentity()), caller is null ? null : InboundToken);
        }

        await scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient(client)
            .GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        return wire.Sent[^1];
    }

    // Forward is the default: the caller's own token, whatever the host's handlers set on the client.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ByDefault_ACallersCall_CarriesTheCallersTokenUnchanged(bool hostCredentialsHandler)
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ServiceTokenHandler>(), hostCredentialsHandler: hostCredentialsHandler);
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", Carol);

        Assert.Equal([InboundToken], sent.Headers.GetValues("Authorization"));
    }

    [Theory]
    [InlineData("Forward")]
    [InlineData("Exchange")]
    public async Task TheServicesOwnCall_CarriesTheHostsHandlerToken(string onBehalfOfCalls)
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ServiceTokenHandler>(), onBehalfOfCalls);
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", caller: null, inbound: false);

        Assert.Equal([ServiceToken], sent.Headers.GetValues("Authorization"));
    }

    [Theory]
    [InlineData("Forward")]
    [InlineData("Exchange")]
    public async Task AnAnonymousCaller_ReachesTheServiceAnonymous_WhateverTheHostSet(string onBehalfOfCalls)
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ExchangeHandler>(), onBehalfOfCalls, hostCredentialsHandler: true);
        using var _ = provider;

        var sent = await GetStateAsync(provider, wire, "Intake", caller: null);

        Assert.False(sent.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task Exchange_EachCallCarriesTheTokenTheHostsHandlerSetForTheCaller()
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ExchangeHandler>(), "Exchange");
        using var _ = provider;

        var toIntake = await GetStateAsync(provider, wire, "Intake", Carol);
        var toMember = await GetStateAsync(provider, wire, "Member", Carol);

        Assert.Equal(["Bearer carol-for-intake.test"], toIntake.Headers.GetValues("Authorization"));
        Assert.Equal(["Bearer carol-for-member.test"], toMember.Headers.GetValues("Authorization"));
    }

    // Exchange never lets the inbound token out unchanged (the MCP rule: no token passthrough).
    [Fact]
    public async Task Exchange_AHandlerThatSendsTheInboundTokenUnchanged_IsRefused()
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<PassThroughHandler>(), "Exchange");
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => GetStateAsync(provider, wire, "Intake", Carol));

        Assert.Equal(HttpClientErrorCodes.CallerTokenNotExchanged, exception.Code);
        Assert.Empty(wire.Sent);
    }

    [Fact]
    public async Task Exchange_ACallerCallWithoutAnExchangedToken_IsRefused()
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ServiceTokenHandler>(), "Exchange");
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => GetStateAsync(provider, wire, "Intake", Carol));

        Assert.Equal(HttpClientErrorCodes.CallerTokenNotExchanged, exception.Code);
        Assert.Empty(wire.Sent);
    }

    // A registry fetch is shared by every caller: the service's own call, even inside a caller's request.
    [Fact]
    public async Task ARegistryFetch_InsideACallersRequest_IsTheServicesOwnCall()
    {
        var (provider, wire) = Build(k => k.AddKaleidoClientHandler<ServiceTokenHandler>(), "Exchange", registryClient: true);
        using var _ = provider;
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IKaleidoInboundCallerAccessor>().Set(Carol, InboundToken);

        await scope.ServiceProvider.GetRequiredService<IKaleidoRegistryClient>()
            .GetCapabilitiesAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal([ServiceToken], wire.Sent[^1].Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task ConcurrentCallers_EachSendTheirOwnToken()
    {
        var (provider, wire) = Build(_ => { });
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
        Assert.Equal(callers.Select(x => $"Bearer {x.Identity?.Name}-inbound").Order(StringComparer.Ordinal), sent);
    }
}
