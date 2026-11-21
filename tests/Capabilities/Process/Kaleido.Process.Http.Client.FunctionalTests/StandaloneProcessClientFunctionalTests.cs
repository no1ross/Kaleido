using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Exceptions;
using Microsoft.Extensions.Time.Testing;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Process.Http.Client.FunctionalTests;

public sealed class StandaloneProcessClientFunctionalTests
{
    [Fact]
    public async Task WorkerHost_UsesPinnedProcessVersionWhileAnotherVersionIsCurrentAndAuthorityStops()
    {
        await using var authority = await StartAuthorityAsync();
        var registeredThree = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var versionThree = await StartPublisherAsync(authority, "3", registeredThree);
        await registeredThree.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var registeredTwo = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var versionTwo = await StartPublisherAsync(authority, "2", registeredTwo);
        await registeredTwo.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var firstClient = versionTwo.GetTestClient();
        firstClient.BaseAddress = new Uri("https://localhost/");
        firstClient.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reviewer"]));
        using var started = await firstClient.PostAsJsonAsync("/catalog/2/processes/steps/revieworderstep",
            new ExecuteStepRequest<Publisher.ReviewOrderStep> { ProcessStep = new Publisher.ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var initial = Assert.IsType<ProcessResult>(await started.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal("2", initial.ServiceVersion);

        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = StartConsumer(authority, versionTwo, versionThree, read);
        using var scope = consumer.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>();
        var unavailable = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => factory.GetClient("catalog", "2")
            .GetProcessStateAsync(initial.ProcessId, TestContext.Current.CancellationToken));
        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, unavailable.Code);

        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var statusClient = authority.GetTestClient();
        statusClient.BaseAddress = new Uri("https://localhost/");
        statusClient.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("status-reader", ["registry-reader"]));
        var status = await statusClient.GetFromJsonAsync<KaleidoRegistryStatus>("/registry/status", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Assert.IsType<KaleidoRegistryStatus>(status).Services, service => service.ServiceName == "reader");
        await authority.StopAsync(TestContext.Current.CancellationToken);

        var pinned = factory.GetClient("catalog", Assert.IsType<string>(initial.ServiceVersion));
        var snapshot = await pinned.GetProcessStateAsync(initial.ProcessId, TestContext.Current.CancellationToken);
        Assert.Equal((initial.ProcessId, "2"), (Assert.IsType<ProcessStateSnapshot>(snapshot).ProcessId, snapshot.ServiceVersion));
        var current = await factory.GetClient("catalog").ExecuteStepAsync(new Consumer.ReviewOrderStep("B-1"), null, "current-version-call", TestContext.Current.CancellationToken);
        var stillPinned = await pinned.ExecuteStepAsync(new Consumer.ReviewOrderStep("C-1"), null, "pinned-version-call", TestContext.Current.CancellationToken);
        Assert.Equal(("3", "2"), (current.ServiceVersion, stillPinned.ServiceVersion));
        var replay = await pinned.ExecuteStepAsync(new Consumer.ReviewOrderStep("C-1"), null, "pinned-version-call", TestContext.Current.CancellationToken);
        Assert.Equal(stillPinned.ProcessId, replay.ProcessId);
        var reused = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => pinned.ExecuteStepAsync(
            new Consumer.ReviewOrderStep("changed"), null, "pinned-version-call", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyKeyReused, Assert.Single(reused.Errors).Code);
        var multi = await pinned.ExecuteAsync(new ProcessRequest
        {
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?> { ["ReviewOrderStep"] = new Consumer.ReviewOrderStep("D-1") },
            },
        }, "multi-step-call", TestContext.Current.CancellationToken);
        var byName = await pinned.ExecuteStepAsync("ReviewOrderStep", new { Code = "E-1" }, null, "named-step-call", TestContext.Current.CancellationToken);
        Assert.Equal(("2", "2"), (multi.ServiceVersion, byName.ServiceVersion));
        Assert.Equal(4, versionTwo.Services.GetRequiredService<Publisher.ExecutionCounter>().Count);
        Assert.Equal(1, versionThree.Services.GetRequiredService<Publisher.ExecutionCounter>().Count);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await versionThree.StopAsync(TestContext.Current.CancellationToken);
        await versionTwo.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExplicitExecuteProcessId_WinsOverAFrontDoorScopeOnTheSameProcessClient()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, "2", registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new ConcurrentQueue<string?>();
        using var consumer = StartConsumer(authority, publisher, publisher, read, onProcess: request =>
        {
            if (request.Method == HttpMethod.Post && request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values))
            {
                sent.Enqueue(values.SingleOrDefault());
            }
        });
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var scope = consumer.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient("catalog", "2");
        var started = await client.ExecuteStepAsync(new Consumer.ReviewOrderStep("A-1"), null, "initial-process", TestContext.Current.CancellationToken);
        var forwardedId = Guid.NewGuid();
        var forwarding = new KaleidoProcessForwarding();

        using (forwarding.ForwardProcess(forwardedId))
        {
            await client.ExecuteStepAsync(new Consumer.ReviewOrderStep("A-1"), started.ProcessId,
                "explicit-process", TestContext.Current.CancellationToken);
        }

        Assert.Equal(started.ProcessId.ToString("D"), Assert.Single(sent));
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task KeyedPost_AfterTransientOrInProgressGatewayAnswer_RetriesTheSameKeyAndRunsOnce(HttpStatusCode firstStatus)
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, "2", registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var probe = new RetryProbe(firstStatus);
        using var consumer = StartConsumer(authority, publisher, publisher, read, time, probe);
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var scope = consumer.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>();
        var keyed = scope.ServiceProvider.GetRequiredKeyedService<IKaleidoProcessorClient>("catalog");
        Assert.Null(await keyed.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        var invalid = Assert.Throws<KaleidoConfigurationException>(() => scope.ServiceProvider.GetRequiredKeyedService<IKaleidoProcessorClient>(42));
        Assert.Equal(HttpClientErrorCodes.UnknownClient, invalid.Code);

        var pending = factory.GetClient("catalog", "2").ExecuteStepAsync(new Consumer.ReviewOrderStep("A-1"), null,
            "retry-pinned-call", TestContext.Current.CancellationToken);
        await probe.FirstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        for (var tick = 0; tick < 5 && !probe.SecondAttempt.Task.IsCompleted && !pending.IsCompleted; tick++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromSeconds(1));
        }

        await Task.WhenAny(probe.SecondAttempt.Task, pending).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(probe.SecondAttempt.Task.IsCompletedSuccessfully);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("2", result.ServiceVersion);
        Assert.Equal(["retry-pinned-call", "retry-pinned-call"], probe.Keys);
        Assert.Equal(1, publisher.Services.GetRequiredService<Publisher.ExecutionCounter>().Count);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AuthenticatedProcessStep_ExchangesCallerTokenAndHandsOffToPinnedTarget()
    {
        await using var authority = await StartAuthorityAsync();
        var registeredThree = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var versionThree = await StartPublisherAsync(authority, "3", registeredThree);
        await registeredThree.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var registeredTwo = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var versionTwo = await StartPublisherAsync(authority, "2", registeredTwo, audience: "catalog-api");
        await registeredTwo.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var proxy = await StartProxyAsync(authority, versionTwo, versionThree, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(proxy);
        using var caller = proxy.GetTestClient();
        caller.BaseAddress = new Uri("https://localhost/");
        caller.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reviewer"]));

        using var response = await caller.PostAsJsonAsync("/reader/1/processes/steps/forwardstep",
            new ExecuteStepRequest<Consumer.ForwardStep> { ProcessStep = new Consumer.ForwardStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var handOff = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("1", "catalog", "2"), (handOff.ServiceVersion, handOff.TargetProcessorName, handOff.TargetProcessorVersion));
        Assert.Equal(1, versionTwo.Services.GetRequiredService<Publisher.ExecutionCounter>().Count);
        Assert.Equal(0, versionThree.Services.GetRequiredService<Publisher.ExecutionCounter>().Count);

        var state = await caller.GetFromJsonAsync<ProcessStateSnapshot>($"/reader/1/processes/{handOff.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("2", Assert.IsType<ProcessStateSnapshot>(state).TargetProcessorVersion);
        await proxy.StopAsync(TestContext.Current.CancellationToken);
        await versionTwo.StopAsync(TestContext.Current.CancellationToken);
        await versionThree.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost StartConsumer(WebApplication authority, WebApplication versionTwo, WebApplication versionThree, TaskCompletionSource<bool> read,
        TimeProvider? time = null, RetryProbe? retry = null, Action<HttpRequestMessage>? onProcess = null) =>
        Host.CreateDefaultBuilder()
            .UseEnvironment("Production")
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:BaseUrl"] = "https://localhost/",
            }))
            .ConfigureServices((context, services) =>
            {
                if (time is not null)
                {
                    services.AddSingleton(time);
                }

                services.AddKaleido(context.Configuration, new()
                {
                    ServiceName = "reader",
                    ServiceVersion = "1",
                    Assemblies = [typeof(StandaloneProcessClientFunctionalTests).Assembly],
                    TypeFilter = _ => false,
                })
                    .AddKaleidoClientHandler<ServiceTokenHandler>()
                    .AddRegistryHttpParticipation()
                    .AddProcessHttpClient();
                services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
                    options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveReadHandler(read)
                    {
                        InnerHandler = authority.GetTestServer().CreateHandler(),
                    }));
                services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
                    options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new VersionSwitchHandler(
                        versionTwo.GetTestServer().CreateHandler(), versionThree.GetTestServer().CreateHandler(), retry, onProcess)));
            })
            .Build();

    private static async Task<WebApplication> StartProxyAsync(WebApplication authority, WebApplication versionTwo, WebApplication versionThree, TaskCompletionSource<bool> read)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        });
        builder.Services.AddTestJwtBearer();
        builder.Services.AddSingleton(new Publisher.StoredProcesses());
        builder.Services.AddSingleton(new Publisher.StoredIdempotency());
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(StandaloneProcessClientFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(Consumer.ForwardStep) || type == typeof(Consumer.ForwardHandler),
        })
            .AddKaleidoClientHandler<ExchangeTokenHandler>()
            .AddRegistryHttpParticipation(new KaleidoClientOptions { OnBehalfOfCalls = KaleidoOnBehalfOfCalls.Exchange })
            .AddEventing()
            .AddProcess()
            .AddContextStore<Publisher.TestProcessContextStore>()
            .AddIdempotencyStore<Publisher.TestIdempotencyStore>()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpBindings()
            .AddProcessHttpServer()
            .AddProcessHttpClient();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveReadHandler(read)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new VersionSwitchHandler(
                versionTwo.GetTestServer().CreateHandler(), versionThree.GetTestServer().CreateHandler())));
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProcessor();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddAuthorization(policies =>
        {
            policies.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
            policies.AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
        });
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(StandaloneProcessClientFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleidoControlRegistry();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartPublisherAsync(WebApplication authority, string version, TaskCompletionSource<bool> registered, string audience = TestTokenIssuer.PlatformAudience)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        });
        builder.Services.AddTestJwtBearer(audience);
        builder.Services.AddSingleton(new Publisher.StoredProcesses());
        builder.Services.AddSingleton(new Publisher.StoredIdempotency());
        builder.Services.AddSingleton<Publisher.ExecutionCounter>();
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = version,
            Assemblies = [typeof(StandaloneProcessClientFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(Publisher.ReviewOrderStep) || type == typeof(Publisher.ReviewOrderHandler),
        })
            .AddKaleidoClientHandler<ServiceTokenHandler>()
            .AddRegistryHttpParticipation()
            .AddEventing()
            .AddProcess()
            .AddContextStore<Publisher.TestProcessContextStore>()
            .AddIdempotencyStore<Publisher.TestIdempotencyStore>()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpBindings()
            .AddProcessHttpServer();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveRegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProcessor();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task WaitUntilReadyAsync(IHost consumer)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var health = consumer.Services.GetRequiredService<HealthCheckService>();
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

    private sealed class ServiceTokenHandler(KaleidoServiceOptions service) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var roles = service.ServiceName == "catalog"
                ? ["registrant", "registry-reader", "Kaleido.Registrant.catalog"]
                : new[] { "registry-reader", "reviewer" };
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App(service.ServiceName + "-app", roles));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ExchangeTokenHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = KaleidoCallContext.Of(request);
            if (call.Purpose == KaleidoCallPurpose.ServiceOwn)
            {
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("reader-proxy",
                    ["registrant", "registry-reader", "Kaleido.Registrant.reader"]));
            }
            else
            {
                var caller = call.Caller?.Identity?.Name ?? "anonymous";
                request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(caller,
                    call.Caller?.IsInRole("reviewer") == true ? ["reviewer"] : [], app: "reader-exchange", audience: "catalog-api"));
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ObserveRegistrationHandler(TaskCompletionSource<bool> registered) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/instances", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                registered.TrySetResult(true);
            }

            return response;
        }
    }

    private sealed class ObserveReadHandler(TaskCompletionSource<bool> read) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Get
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/capabilities", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                read.TrySetResult(true);
            }

            return response;
        }
    }

    private sealed class RetryProbe(HttpStatusCode status)
    {
        private int _rejectFirst = 1;
        public HttpStatusCode Status { get; } = status;
        public TaskCompletionSource<bool> FirstAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<string> Keys { get; } = new();

        public bool Reject(HttpRequestMessage request)
        {
            if (!request.Headers.TryGetValues(KaleidoIdempotencyHeaders.IdempotencyKey, out var values)
                || Assert.Single(values) is not "retry-pinned-call")
            {
                return false;
            }

            Keys.Enqueue("retry-pinned-call");
            if (Interlocked.Exchange(ref _rejectFirst, 0) != 1)
            {
                SecondAttempt.TrySetResult(true);
                return false;
            }

            FirstAttempt.TrySetResult(true);
            return true;
        }
    }

    private sealed class VersionSwitchHandler(HttpMessageHandler two, HttpMessageHandler three, RetryProbe? retry = null,
        Action<HttpRequestMessage>? observe = null) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _two = new(two);
        private readonly HttpMessageInvoker _three = new(three);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            observe?.Invoke(request);
            if (retry?.Reject(request) == true)
            {
                var response = new HttpResponseMessage(retry.Status) { RequestMessage = request };
                if (retry.Status == HttpStatusCode.Conflict)
                {
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                }

                return Task.FromResult(response);
            }

            return request.RequestUri?.AbsolutePath.StartsWith("/catalog/2/", StringComparison.Ordinal) == true
                ? _two.SendAsync(request, cancellationToken)
                : _three.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _two.Dispose();
                _three.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
