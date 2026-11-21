using System.Net;
using System.Net.Http.Json;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.Processes;
using Kaleido.Testing.Identity;
using Kaleido.Registry.Http.Client;
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
