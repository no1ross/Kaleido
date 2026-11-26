using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Http.Client.Calls;
using Kaleido.Http.Client.Configuration;
using Kaleido.Http.Client.Registration;
using Kaleido.Process.Client;
using Kaleido.Process.Http.Client;
using Kaleido.Queryable.Http;
using Kaleido.Registry.Http.Client;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;

namespace Kaleido.Hosting.Http.FunctionalTests.CallerCredentials;

public sealed class HandlerAndSourceCallFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AStepHandlersCall_CarriesTheCallersToken_ByDefault()
    {
        await using var host = await RelayHost.StartAsync(exchange: false);
        var token = TestTokenIssuer.User("bob", ["radiology"]);

        var status = await host.ExecuteStepAsync(token);

        Assert.Equal(HttpStatusCode.OK, status);
        var sent = host.SingleCall();
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, sent.Purpose);
        Assert.Equal(0, string.CompareOrdinal("Bearer " + token, sent.Authorization));
    }

    [Fact]
    public async Task ADelegatedSourcesCall_CarriesTheCallersToken_ByDefault()
    {
        await using var host = await RelayHost.StartAsync(exchange: false);
        var token = TestTokenIssuer.User("bob");

        var status = await host.QuerySourceAsync(token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, string.CompareOrdinal("Bearer " + token, host.SingleCall().Authorization));
    }

    [Fact]
    public async Task Exchange_AStepHandlersCall_CarriesTheTokenExchangedForTheCaller()
    {
        await using var host = await RelayHost.StartAsync(exchange: true);
        var incoming = TestTokenIssuer.User("bob", tokenId: Guid.NewGuid().ToString("N"));

        var status = await host.ExecuteStepAsync(incoming);

        Assert.Equal(HttpStatusCode.OK, status);
        var sent = host.SingleCall();
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, sent.Purpose);
        Assert.NotEqual(0, string.CompareOrdinal("Bearer " + incoming, sent.Authorization));
        Assert.True(sent.Authorization?.StartsWith("Bearer ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAnonymousCaller_StaysAnonymous_InAStepHandlersCall(bool exchange)
    {
        await using var host = await RelayHost.StartAsync(exchange);

        var status = await host.ExecuteStepAsync(token: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null(host.SingleCall().Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCallers_EachCallCarriesItsOwnCaller(bool exchange)
    {
        await using var host = await RelayHost.StartAsync(exchange);
        var callers = Enumerable.Range(0, 16).Select(index =>
            (Name: $"user{index}", Token: TestTokenIssuer.User($"user{index}", tokenId: Guid.NewGuid().ToString("N")))).ToArray();

        var statuses = await Task.WhenAll(callers.Select(caller => host.ExecuteStepAsync(caller.Token)));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var sent = host.Wire.Sent.ToArray();
        Assert.Equal(callers.Length, sent.Length);
        foreach (var caller in callers)
        {
            Assert.Equal(1, sent.Count(call => call.CallerName == caller.Name));
            var outgoing = sent.Single(call => call.CallerName == caller.Name);
            if (exchange)
            {
                Assert.NotEqual(0, string.CompareOrdinal("Bearer " + caller.Token, outgoing.Authorization));
                Assert.True(outgoing.Authorization?.StartsWith("Bearer ", StringComparison.Ordinal));
            }
            else
            {
                Assert.Equal(0, string.CompareOrdinal("Bearer " + caller.Token, outgoing.Authorization));
            }
        }
    }

    private sealed class RelayHost(WebApplication authority, WebApplication downstream, WebApplication relay, CaptureProcessHandler wire) : IAsyncDisposable
    {
        public CaptureProcessHandler Wire { get; } = wire;

        public (KaleidoCallPurpose Purpose, string? CallerName, string? Authorization) SingleCall()
        {
            var sent = Wire.Sent.ToArray();
            Assert.InRange(sent.Length, 1, 1);
            return sent[0];
        }

        public static async Task<RelayHost> StartAsync(bool exchange)
        {
            var authority = await StartAuthorityAsync();
            var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var downstream = await StartDownstreamAsync(authority, registered);
            await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
            var wire = new CaptureProcessHandler { InnerHandler = downstream.GetTestServer().CreateHandler() };
            var relay = await StartRelayAsync(authority, wire, exchange);
            await WaitUntilReadyAsync(relay);
            return new RelayHost(authority, downstream, relay, wire);
        }

        public Task<HttpStatusCode> ExecuteStepAsync(string? token) =>
            PostAsync("/relay/1/processes/steps/relaystep", new { processStep = new { } }, token);

        public Task<HttpStatusCode> QuerySourceAsync(string? token) =>
            PostAsync("/relay/1/queryable/delegatedrecordsource/query", new { query = new { } }, token);

        private async Task<HttpStatusCode> PostAsync(string url, object body, string? token)
        {
            using var client = relay.GetTestClient();
            client.BaseAddress = new Uri("https://localhost/");
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(token);
            }

            using var response = await client.PostAsJsonAsync(url, body, KaleidoJsonOptions.Options, Cancel);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            await relay.StopAsync(CancellationToken.None);
            await relay.DisposeAsync();
            await downstream.StopAsync(CancellationToken.None);
            await downstream.DisposeAsync();
            await authority.StopAsync(CancellationToken.None);
            await authority.DisposeAsync();
        }
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(HandlerAndSourceCallFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry()
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.MapKaleido().MapCentralRegistry();
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<WebApplication> StartDownstreamAsync(WebApplication authority, TaskCompletionSource registered)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "downstream",
            ServiceVersion = "1",
            Assemblies = [typeof(HandlerAndSourceCallFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(DownstreamStep) || type == typeof(DownstreamHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer()
            .AddProcessHttpBindings()
            .AddRegistryHttpParticipation();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleido().MapProcess();
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<WebApplication> StartRelayAsync(WebApplication authority, CaptureProcessHandler wire, bool exchange)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        var kaleido = builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "relay",
            ServiceVersion = "1",
            Assemblies = [typeof(HandlerAndSourceCallFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(RelayStep) || type == typeof(RelayHandler) || type == typeof(DelegatedRecordSource),
        })
            .AddEventing()
            .AddProcess()
            .AddQueryable()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer()
            .AddProcessHttpBindings()
            .AddQueryableHttpBindings();
        if (exchange)
        {
            kaleido.AddKaleidoClientHandler<SignedExchangeHandler>();
        }

        kaleido.AddRegistryHttpParticipation(new KaleidoClientOptions
        {
            OnBehalfOfCalls = exchange ? KaleidoOnBehalfOfCalls.Exchange : KaleidoOnBehalfOfCalls.Forward,
        }).AddProcessHttpClient();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = authority.GetTestServer().CreateHandler()));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = wire));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleido().MapProcess().MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task WaitUntilReadyAsync(WebApplication relay)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var health = relay.Services.GetRequiredService<HealthCheckService>();
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var report = await health.CheckHealthAsync(check => check.Name == "kaleido-registration", timeout.Token);
            if (report.Status == HealthStatus.Healthy)
            {
                return;
            }

            await Task.Yield();
        }
    }

    private sealed class SignedExchangeHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);
            if (call.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && call.Caller is { } caller)
            {
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(caller.Identity?.Name ?? "anonymous",
                    app: "relay-exchange", tokenId: Guid.NewGuid().ToString("N")));
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class CaptureProcessHandler : DelegatingHandler
    {
        public ConcurrentQueue<(KaleidoCallPurpose Purpose, string? CallerName, string? Authorization)> Sent { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);
            Sent.Enqueue((call.Purpose, call.Caller?.Identity?.Name, request.Headers.Authorization?.ToString()));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RegistrationHandler(TaskCompletionSource registered) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/registry/instances" && response.IsSuccessStatusCode)
            {
                registered.TrySetResult();
            }

            return response;
        }
    }

    [ProcessStep(DisplayName = "Downstream step", Description = "Publishes a process-state HTTP binding")]
    public sealed record DownstreamStep : IProcessStep;

    public sealed class DownstreamHandler : IProcessStepHandler<DownstreamStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(DownstreamStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Relay to downstream", Description = "Calls another service from a step handler")]
    public sealed record RelayStep : IProcessStep;

    public sealed class RelayHandler(IKaleidoProcessorClientFactory clients) : IProcessStepHandler<RelayStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(RelayStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            await clients.GetClient("downstream").GetProcessStateAsync(Guid.NewGuid(), cancellationToken);
            return ProcessStepHandlerResult.Success();
        }
    }

    public sealed record DelegatedRecord : IQueryContext
    {
        [Description("The record code")]
        [Filterable(FilterOperator.Equals)]
        public string Code { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Downstream records", Description = "Reads another service in the caller's scope")]
    public sealed class DelegatedRecordSource(IKaleidoProcessorClientFactory clients) : IDelegatedQuerySource<DelegatedRecord, DelegatedRecord>
    {
        public async Task<QueryResult<DelegatedRecord>> ExecuteAsync(IQueryRequest<EmptyQueryViewParameters> request, CancellationToken cancellationToken)
        {
            await clients.GetClient("downstream").GetProcessStateAsync(Guid.NewGuid(), cancellationToken);
            return new QueryResult<DelegatedRecord>(0, 0, 0, []);
        }
    }
}
