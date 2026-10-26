using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.IntegrationTests.CallerCredentials;

/// <summary>
/// #428 through a real host: a step handler and a delegated source that call another service while
/// handling a caller's request make that call for the caller (ADR 0022), as an MCP tool call does.
/// They run in their own DI scope, which must know the inbound caller as it knows the correlation;
/// otherwise the call goes out as the service's own and the downstream refuses user-only steps.
/// </summary>
public sealed class HandlerAndSourceCallTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AStepHandlersCall_IsMadeForTheCaller()
    {
        await using var host = await RelayHost.StartAsync();

        var status = await host.ExecuteRelayStepAsync(caller: "bob|radiology");

        Assert.Equal(HttpStatusCode.OK, status);
        var asked = Assert.Single(host.Credentials.Asked);
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, asked.Purpose);
        Assert.Equal("bob", asked.Caller?.Identity?.Name);
        Assert.Equal("Test bob|radiology", asked.CallerAuthorization);
        Assert.Equal(["Bearer bob-for-downstream"], Assert.Single(host.Wire.Sent).Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task ADelegatedSourcesCall_IsMadeForTheCaller()
    {
        await using var host = await RelayHost.StartAsync();

        var status = await host.QueryRelaySourceAsync(caller: "bob");

        Assert.Equal(HttpStatusCode.OK, status);
        var asked = Assert.Single(host.Credentials.Asked);
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, asked.Purpose);
        Assert.Equal("bob", asked.Caller?.Identity?.Name);
        Assert.Equal(["Bearer bob-for-downstream"], Assert.Single(host.Wire.Sent).Headers.GetValues("Authorization"));
    }

    // An anonymous caller reaches the downstream anonymous: never with the service's credential.
    [Fact]
    public async Task AnAnonymousCaller_StaysAnonymous_InAStepHandlersCall()
    {
        await using var host = await RelayHost.StartAsync();

        var status = await host.ExecuteRelayStepAsync(caller: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(host.Credentials.Asked);
        Assert.False(Assert.Single(host.Wire.Sent).Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task ConcurrentCallers_EachGetTheirOwnCredential_InStepHandlersCalls()
    {
        await using var host = await RelayHost.StartAsync();
        var callers = Enumerable.Range(0, 16).Select(i => $"user{i}").ToArray();

        var statuses = await Task.WhenAll(callers.Select(host.ExecuteRelayStepAsync));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var sent = host.Wire.Sent.Select(x => x.Headers.GetValues("Authorization").Single()).Order(StringComparer.Ordinal);
        Assert.Equal(callers.Select(x => $"Bearer {x}-for-downstream").Order(StringComparer.Ordinal), sent);
    }

    // The relay service: its step handler and its delegated source call the "Downstream" service,
    // whose wire is captured; the credential provider stands in for a token exchange.
    private sealed class RelayHost : IAsyncDisposable
    {
        private readonly IHost _host;

        private RelayHost(IHost host, ExchangeProvider credentials, CaptureHandler wire)
        {
            _host = host;
            Credentials = credentials;
            Wire = wire;
        }

        public ExchangeProvider Credentials { get; }

        public CaptureHandler Wire { get; }

        public static async Task<RelayHost> StartAsync()
        {
            var credentials = new ExchangeProvider();
            var wire = new CaptureHandler();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kaleido:Clients:Downstream:BaseUrl"] = "http://downstream.test/",
                    ["Kaleido:Clients:Downstream:RoutePrefix"] = "downstream",
                })
                .Build();

            var host = await new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(s =>
                    {
                        s.AddRouting();
                        s.AddAuthentication(TestAuthentication.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
                        s.AddAuthorization();

                        s.AddKaleido(configuration, o =>
                            {
                                o.ServiceName = "relay";
                                o.Assemblies = [typeof(HandlerAndSourceCallTests).Assembly];
                                o.TypeFilter = type => type.Namespace == typeof(HandlerAndSourceCallTests).Namespace;
                                o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            })
                            .AddInMemoryStores()
                            .AddHttp()
                            .AddHttpClients();

                        s.AddSingleton<IKaleidoCallCredentialProvider>(credentials);
                        s.AddHttpClient("Downstream").ConfigurePrimaryHttpMessageHandler(() => wire);
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

            return new RelayHost(host, credentials, wire);
        }

        public Task<HttpStatusCode> ExecuteRelayStepAsync(string? caller) =>
            PostAsync($"/relay/processes/steps/{nameof(RelayToDownstreamStep)}", new { processStep = new { } }, caller);

        public Task<HttpStatusCode> QueryRelaySourceAsync(string? caller) =>
            PostAsync($"/relay/queryable/{nameof(DownstreamRecordSource)}/query", new { }, caller);

        private async Task<HttpStatusCode> PostAsync(string url, object body, string? caller)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
            };

            if (caller is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Test {caller}");
            }

            using var client = _host.GetTestClient();
            using var response = await client.SendAsync(request, Cancel);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(Cancel);
            _host.Dispose();
        }
    }

    // A token exchange stand-in: a token for the caller addressed to the destination, a service token for own calls.
    private sealed class ExchangeProvider : IKaleidoCallCredentialProvider
    {
        private readonly List<KaleidoCallCredentialRequest> _asked = [];

        public IReadOnlyList<KaleidoCallCredentialRequest> Asked
        {
            get
            {
                lock (_asked)
                {
                    return [.. _asked];
                }
            }
        }

        public ValueTask<KaleidoCallCredential> GetCredentialAsync(KaleidoCallCredentialRequest request, CancellationToken cancellationToken)
        {
            lock (_asked)
            {
                _asked.Add(request);
            }

            return ValueTask.FromResult(KaleidoCallCredential.Header(
                request.Purpose == KaleidoCallPurpose.OnBehalfOfCaller
                    ? $"Bearer {request.Caller?.Identity?.Name}-for-{request.ServiceName}"
                    : $"Bearer svc-for-{request.ServiceName}"));
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

    // Authorization: Test name|role,role signs the caller in; no header is anonymous.
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();

            if (!header.StartsWith("Test ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Test ".Length..].Split('|');
            var claims = new List<Claim> { new(ClaimTypes.Name, parts[0]) };
            claims.AddRange((parts.Length > 1 ? parts[1] : string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim(ClaimTypes.Role, role)));

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
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
