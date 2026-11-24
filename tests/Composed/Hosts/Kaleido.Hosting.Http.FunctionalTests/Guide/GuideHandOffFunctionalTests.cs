using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Guide.Examples.Callers;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.InformationRequests;
using Kaleido.Guide.Examples.Processes;
using Kaleido.Http.Client.Calls;
using Kaleido.Http.Client.Configuration;
using Kaleido.Http.Client.Registration;
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

namespace Kaleido.Hosting.Http.FunctionalTests.Guide;

public sealed class GuideHandOffFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHandOff_StartsTheSameProcessOnReturns_AndCanContinueThere()
    {
        await using var authority = await StartAuthorityAsync();
        var returnsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var returns = await StartReturnsAsync(authority, returnsRegistered);
        await returnsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var ordersRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var orders = await StartOrdersAsync(authority, returns, ordersRegistered);
        await ordersRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForRegistryAsync(orders);

        using var caller = orders.GetTestClient();
        caller.BaseAddress = new Uri("https://localhost/");
        caller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice"));
        var placed = await ExecuteAsync(caller, "orders", "placeorderstep", new { item = "Book" }, null);
        var processId = placed.ProcessId;
        await ExecuteAsync(caller, "orders", "confirmorderstep", new { }, processId);
        await ExecuteAsync(caller, "orders", "shiporderstep", new { carrier = "post", address = "1 Main St" }, processId);

        var handedOff = await ExecuteAsync(caller, "orders", "returnorderstep", new ReturnOrderStep(), processId);

        Assert.Equal(("returns", "1"), (handedOff.TargetProcessorName, handedOff.TargetProcessorVersion));
        Assert.Contains(handedOff.Steps, step => step.StepName == nameof(ReturnOrderStep) && step.ExecutionStatus == StepExecutionStatus.Completed);
        var late = await ExecuteAsync(caller, "orders", "giftwrapstep", new GiftWrapStep(), processId);
        Assert.Equal(StepExecutionStatus.Pending, Assert.Single(late.Steps).ExecutionStatus);
        var origin = Assert.IsType<ProcessStateSnapshot>(await caller.GetFromJsonAsync<ProcessStateSnapshot>($"/orders/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(ProcessExecutionState.HandOff, origin.State);
        Assert.Equal(("returns", "1"), (origin.TargetProcessorName, origin.TargetProcessorVersion));
        Assert.DoesNotContain(origin.Steps, step => step.StepName == nameof(GiftWrapStep) && step.Status == StepExecutionStatus.Completed);
        using var returnsCaller = returns.GetTestClient();
        returnsCaller.BaseAddress = new Uri("https://localhost/");
        returnsCaller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice"));
        var state = await returnsCaller.GetFromJsonAsync<ProcessStateSnapshot>($"/returns/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(processId, Assert.IsType<ProcessStateSnapshot>(state).ProcessId);
        Assert.Contains(state.Steps, step => step.StepName == nameof(OpenReturnStep) && step.Status == StepExecutionStatus.Completed);

        var received = await ExecuteAsync(returnsCaller, "returns", "receivereturnstep", new ReceiveReturnStep(), processId);
        Assert.Equal(processId, received.ProcessId);
        Assert.Contains(received.Steps, step => step.StepName == nameof(ReceiveReturnStep) && step.ExecutionStatus == StepExecutionStatus.Completed);
    }

    [Fact]
    public async Task AHandBackFromTheNamedTarget_ReopensTheOriginUnderTheSameProcessId()
    {
        await using var authority = await StartAuthorityAsync();
        var returnsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var returns = await StartReturnsAsync(authority, returnsRegistered);
        await returnsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var ordersRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var orders = await StartOrdersAsync(authority, returns, ordersRegistered);
        await ordersRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForRegistryAsync(orders);
        using var caller = orders.GetTestClient();
        caller.BaseAddress = new Uri("https://localhost/");
        caller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice"));
        var placed = await ExecuteAsync(caller, "orders", "placeorderstep", new { item = "Book" }, null);
        await ExecuteAsync(caller, "orders", "confirmorderstep", new { }, placed.ProcessId);
        await ExecuteAsync(caller, "orders", "shiporderstep", new { carrier = "post", address = "1 Main St" }, placed.ProcessId);
        var handedOff = await ExecuteAsync(caller, "orders", "returnorderstep", new ReturnOrderStep(), placed.ProcessId);
        Assert.Equal(ProcessExecutionState.HandOff, handedOff.State);

        using var handBackCaller = orders.GetTestClient();
        handBackCaller.BaseAddress = new Uri("https://localhost/");
        handBackCaller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("returns-app"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders/1/processes/steps/giftwrapstep")
        {
            Content = JsonContent.Create(new { processStep = new GiftWrapStep() }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, placed.ProcessId.ToString("D"));
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, "RETURNS");
        using var response = await handBackCaller.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resumed = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(placed.ProcessId, resumed.ProcessId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(resumed.Steps).ExecutionStatus);
        Assert.Null(resumed.TargetProcessorName);
        var reopened = Assert.IsType<ProcessStateSnapshot>(await caller.GetFromJsonAsync<ProcessStateSnapshot>($"/orders/1/processes/{placed.ProcessId:D}", KaleidoJsonOptions.Options, Cancel));
        Assert.NotEqual(ProcessExecutionState.HandOff, reopened.State);
        Assert.Null(reopened.TargetProcessorName);
        Assert.Null(reopened.TargetProcessorVersion);
        Assert.Contains(reopened.Steps, step => step.StepName == nameof(GiftWrapStep) && step.Status == StepExecutionStatus.Completed);
    }

    [Fact]
    public async Task TheGuideHandler_ExchangesTheCallerTokenButNotTheRegistryRead()
    {
        await using var authority = await StartAuthorityAsync();
        var returnsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var returns = await StartReturnsAsync(authority, returnsRegistered);
        await returnsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var ordersRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchange = new SignedTokenExchange();
        var sent = new ConcurrentQueue<(KaleidoCallPurpose Purpose, bool Exchanged, bool Forwarded)>();
        await using var orders = await StartOrdersWithExchangeAsync(authority, returns, ordersRegistered, exchange, sent);
        await ordersRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForRegistryAsync(orders);
        Assert.Equal(0, exchange.Calls);
        using var caller = orders.GetTestClient();
        caller.BaseAddress = new Uri("https://localhost/");
        caller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", tokenId: Guid.NewGuid().ToString("N")));
        var placed = await ExecuteAsync(caller, "orders", "placeorderstep", new { item = "Book" }, null);
        await ExecuteAsync(caller, "orders", "confirmorderstep", new { }, placed.ProcessId);
        await ExecuteAsync(caller, "orders", "shiporderstep", new { carrier = "post", address = "1 Main St" }, placed.ProcessId);

        var handedOff = await ExecuteAsync(caller, "orders", "returnorderstep", new ReturnOrderStep(), placed.ProcessId);

        Assert.Equal(("returns", "1"), (handedOff.TargetProcessorName, handedOff.TargetProcessorVersion));
        Assert.Equal(1, exchange.Calls);
        Assert.Equal(0, string.CompareOrdinal(caller.DefaultRequestHeaders.Authorization?.ToString(), exchange.Inbound));
        var outbound = Assert.Single(sent);
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, outbound.Purpose);
        Assert.True(outbound.Exchanged);
        Assert.False(outbound.Forwarded);
    }

    private static async Task<ProcessResult> ExecuteAsync(HttpClient client, string service, string step, object input, Guid? processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{service}/1/processes/steps/{step}")
        {
            Content = JsonContent.Create(new { processStep = input }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        using var response = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(GuideHandOffFunctionalTests).Assembly],
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

    private static async Task<WebApplication> StartReturnsAsync(WebApplication authority, TaskCompletionSource registered)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "returns",
            ServiceVersion = "1",
            Assemblies = [typeof(GuideHandOffFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(OpenReturnStep) || type == typeof(ReceiveReturnStep)
                || type == typeof(OpenReturnHandler) || type == typeof(ReceiveReturnHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
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

    private static async Task<WebApplication> StartOrdersAsync(WebApplication authority, WebApplication returns, TaskCompletionSource registered)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        OrdersService.AddOrders(builder.Services, builder.Configuration);
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = returns.GetTestServer().CreateHandler()));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        OrdersService.MapOrders(app);
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<WebApplication> StartOrdersWithExchangeAsync(WebApplication authority, WebApplication returns,
        TaskCompletionSource registered, SignedTokenExchange exchange, ConcurrentQueue<(KaleidoCallPurpose Purpose, bool Exchanged, bool Forwarded)> sent)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        builder.Services.AddSingleton<OrderBook>();
        builder.Services.AddSingleton<ITokenExchange>(exchange);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
        })
            .AddEventing()
            .AddProcess()
            .AddQueryable()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpServer()
            .AddProcessHttpBindings()
            .AddQueryableHttpBindings()
            .AddKaleidoClientHandler<TokenExchangeHandler>()
            .AddRegistryHttpParticipation(new KaleidoClientOptions { OnBehalfOfCalls = KaleidoOnBehalfOfCalls.Exchange })
            .AddProcessHttpClient();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new CaptureProcessHandler(exchange, sent)
            {
                InnerHandler = returns.GetTestServer().CreateHandler(),
            }));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleido().MapProcess().MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task WaitForRegistryAsync(WebApplication orders)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var health = orders.Services.GetRequiredService<HealthCheckService>();
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

    private sealed class SignedTokenExchange : ITokenExchange
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public string? Inbound { get; private set; }
        public string? Outbound { get; private set; }

        public Task<string> ExchangeAsync(string callerAuthorization, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            Inbound = callerAuthorization;
            Outbound = "Bearer " + TestTokenIssuer.User("alice", app: "orders-exchange", tokenId: Guid.NewGuid().ToString("N"));
            return Task.FromResult(Outbound);
        }
    }

    private sealed class CaptureProcessHandler(SignedTokenExchange exchange,
        ConcurrentQueue<(KaleidoCallPurpose Purpose, bool Exchanged, bool Forwarded)> sent) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorization = request.Headers.Authorization?.ToString();
            sent.Enqueue((KaleidoCallContext.Of(request).Purpose,
                exchange.Outbound is not null && string.Equals(exchange.Outbound, authorization, StringComparison.Ordinal),
                exchange.Inbound is not null && string.Equals(exchange.Inbound, authorization, StringComparison.Ordinal)));
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

    [ProcessStep(DisplayName = "Open return", Description = "Opens a return after the orders service hands off")]
    public sealed record OpenReturnStep : IProcessStep;

    [ProcessStep(DisplayName = "Receive return", Description = "Records the returned goods")]
    [DependsOn<OpenReturnStep>]
    public sealed record ReceiveReturnStep : IProcessStep;

    public sealed class OpenReturnHandler : IProcessStepHandler<OpenReturnStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(OpenReturnStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class ReceiveReturnHandler : IProcessStepHandler<ReceiveReturnStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(ReceiveReturnStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
