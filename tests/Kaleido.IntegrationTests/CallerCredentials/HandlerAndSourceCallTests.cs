using System.ComponentModel;
using System.Net;
using Kaleido.Client.Processor;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Platform.Processor;
using Kaleido.Platform.Registry;
using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.IntegrationTests.CallerCredentials;

/// <summary>
/// Through a real host authenticating real bearer tokens (#428, #574): a step handler and a delegated source
/// that call another service while handling a caller's request make that call for the caller. They run in
/// their own DI scope, which must know the inbound caller as it knows the correlation. By default the call
/// carries the caller's token unchanged (<c>Forward</c>); with <c>Exchange</c> it carries the token the host's
/// handler exchanged for the caller, never the inbound one.
/// </summary>
public sealed class HandlerAndSourceCallTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AStepHandlersCall_CarriesTheCallersToken_ByDefault()
    {
        await using var host = await RelayHost.StartAsync(onBehalfOfCalls: null);
        var bob = TestTokenIssuer.User("bob", ["radiology"]);

        var status = await host.ExecuteRelayStepAsync(bob);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([$"Bearer {bob}"], Assert.Single(host.Wire.Sent).Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task ADelegatedSourcesCall_CarriesTheCallersToken_ByDefault()
    {
        await using var host = await RelayHost.StartAsync(onBehalfOfCalls: null);
        var bob = TestTokenIssuer.User("bob");

        var status = await host.QueryRelaySourceAsync(bob);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([$"Bearer {bob}"], Assert.Single(host.Wire.Sent).Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task Exchange_AStepHandlersCall_CarriesTheTokenExchangedForTheCaller()
    {
        await using var host = await RelayHost.StartAsync("Exchange");

        var status = await host.ExecuteRelayStepAsync(TestTokenIssuer.User("bob"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["Bearer exchanged-for-bob"], Assert.Single(host.Wire.Sent).Headers.GetValues("Authorization"));
    }

    // An anonymous caller reaches the downstream anonymous: never with a credential the host's handler set.
    [Theory]
    [InlineData(null)]
    [InlineData("Exchange")]
    public async Task AnAnonymousCaller_StaysAnonymous_InAStepHandlersCall(string? onBehalfOfCalls)
    {
        await using var host = await RelayHost.StartAsync(onBehalfOfCalls);

        var status = await host.ExecuteRelayStepAsync(token: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(Assert.Single(host.Wire.Sent).Headers.Contains("Authorization"));
    }

    [Theory]
    [InlineData(null, "Bearer {0}")]
    [InlineData("Exchange", "Bearer exchanged-for-{1}")]
    public async Task ConcurrentCallers_EachCallCarriesItsOwnCaller(string? onBehalfOfCalls, string expected)
    {
        await using var host = await RelayHost.StartAsync(onBehalfOfCalls);
        var callers = Enumerable.Range(0, 16).Select(i => (Name: $"user{i}", Token: TestTokenIssuer.User($"user{i}"))).ToArray();

        var statuses = await Task.WhenAll(callers.Select(x => host.ExecuteRelayStepAsync(x.Token)));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var sent = host.Wire.Sent.Select(x => x.Headers.GetValues("Authorization").Single()).Order(StringComparer.Ordinal);
        Assert.Equal(callers.Select(x => string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, x.Token, x.Name)).Order(StringComparer.Ordinal), sent);
    }

    // The relay service: its step handler and its delegated source call the "Downstream" service, whose wire
    // is captured. It authenticates real bearer tokens, as Production, and calls over https.
    private sealed class RelayHost : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly TestControlRegistry _registry;

        private RelayHost(IHost host, TestControlRegistry registry, CaptureHandler wire)
        {
            _host = host;
            _registry = registry;
            Wire = wire;
        }

        public CaptureHandler Wire { get; }

        public static async Task<RelayHost> StartAsync(string? onBehalfOfCalls)
        {
            var wire = new CaptureHandler();
            var registry = await TestControlRegistry.StartAsync();
            using (var registryClient = registry.CreateClient())
            {
                using var registered = await registryClient.PostAsJsonAsync("/registry/instances", new RegistryInstanceRegistration
                {
                    ServiceName = "downstream",
                    ServiceVersion = "1",
                    InstanceId = Guid.NewGuid(),
                    Processes = [new ProcessorRegistryResponse { Name = "downstream", ServiceName = "downstream", ServiceVersion = "1", ProcessStateUrlTemplate = "/downstream/1/processes/{processId}" }],
                }, KaleidoJsonOptions.Options, Cancel);
                registered.EnsureSuccessStatusCode();
            }

            var settings = new Dictionary<string, string?>
            {
                ["Kaleido:BaseUrl"] = "http://registry.test/",
            };

            if (onBehalfOfCalls is not null)
            {
                settings["Kaleido:OnBehalfOfCalls"] = onBehalfOfCalls;
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var host = await new HostBuilder()
                .UseEnvironment(Environments.Production)
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(s =>
                    {
                        s.AddRouting();
                        s.AddTestJwtBearer();

                        s.AddKaleido(configuration, new()
                        {
                            ServiceName = "relay",
                            ServiceVersion = "1",
                            Assemblies = [typeof(HandlerAndSourceCallTests).Assembly],
                            TypeFilter = type => type.Namespace == typeof(HandlerAndSourceCallTests).Namespace,
                            AuthorizationMode = KaleidoAuthorizationMode.None,
                        })
                            .AddInMemoryStores()
                            .AddHttp()
                            .AddHttpClients()
                            .AddKaleidoClientHandler<NameExchangeHandler>()
                            .AddControlRegistryClient();

                        s.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(registry.CreateHandler);

                        s.AddHttpClient("kaleido-gateway").ConfigurePrimaryHttpMessageHandler(() => wire);
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                    });
                })
                .StartAsync(Cancel);

            await registry.WaitForRegistrationAsync(host);
            return new RelayHost(host, registry, wire);
        }

        public Task<HttpStatusCode> ExecuteRelayStepAsync(string? token) =>
            PostAsync($"/relay/1/processes/steps/{nameof(RelayToDownstreamStep)}", new { processStep = new { } }, token);

        public Task<HttpStatusCode> QueryRelaySourceAsync(string? token) =>
            PostAsync($"/relay/1/queryable/{nameof(DownstreamRecordSource)}/query", new { }, token);

        private async Task<HttpStatusCode> PostAsync(string url, object body, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
            };

            if (token is not null)
            {
                request.Headers.Authorization = TestJwtAuthentication.Bearer(token);
            }

            using var client = _host.GetTestClient();
            client.BaseAddress = new Uri("https://localhost/");
            using var response = await client.SendAsync(request, Cancel);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(Cancel);
            _host.Dispose();
            await _registry.DisposeAsync();
        }
    }

    // A token exchange stand-in on the Kaleido clients: a token naming the caller, for calls made for a caller.
    private sealed class NameExchangeHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);

            if (call.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && call.Caller is { } caller)
            {
                request.Headers.Remove("Authorization");
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer exchanged-for-{caller.Identity?.Name}");
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // The downstream's wire: every request is recorded and answered with an empty process state.
    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _sent = [];

        public IReadOnlyList<HttpRequestMessage> Sent
        {
            get
            {
                lock (_sent)
                {
                    return [.. _sent];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_sent)
            {
                _sent.Add(request);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ProcessStateResponse { ProcessId = Guid.Empty }, options: KaleidoJsonOptions.Options),
            });
        }
    }
}

[ProcessStep(
    DisplayName = "Relay to downstream",
    Description = "Calls the downstream service while handling the step.")]
public sealed record RelayToDownstreamStep : IProcessStep;

public sealed class RelayToDownstreamHandler(IKaleidoProcessorClientFactory clients) : IProcessStepHandler<RelayToDownstreamStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        RelayToDownstreamStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        await clients.GetClient("Downstream").GetProcessStateAsync(Guid.NewGuid(), cancellationToken);
        return ProcessStepHandlerResult.Success();
    }
}

public sealed record DownstreamRecordContext : IQueryContext
{
    [Description("The record's code.")]
    [Filterable(FilterOperator.Equals)]
    public string Code { get; init; } = string.Empty;
}

[QuerySource(
    DisplayName = "Downstream records",
    Description = "Records the downstream service holds, fetched per query.")]
public sealed class DownstreamRecordSource(IKaleidoProcessorClientFactory clients)
    : IDelegatedQuerySource<DownstreamRecordContext, DownstreamRecordContext>
{
    public async Task<QueryResult<DownstreamRecordContext>> ExecuteAsync(
        IQueryRequest<EmptyQueryViewParameters> request,
        CancellationToken cancellationToken)
    {
        await clients.GetClient("Downstream").GetProcessStateAsync(Guid.NewGuid(), cancellationToken);
        return new QueryResult<DownstreamRecordContext>(0, 0, 0, []);
    }
}
