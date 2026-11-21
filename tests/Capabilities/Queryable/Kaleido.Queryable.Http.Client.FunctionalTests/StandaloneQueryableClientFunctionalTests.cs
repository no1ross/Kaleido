using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Queryable.Http.Client.FunctionalTests;

public sealed class StandaloneQueryableClientFunctionalTests
{
    [Fact]
    public async Task WorkerHost_HydratesAtStartupAndQueriesPublishedSourceAfterAuthorityStops()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = StartConsumer(authority, publisher, read);
        using var scope = consumer.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
        var client = factory.GetClient("catalog");
        var unavailable = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() =>
            client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken));
        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, unavailable.Code);

        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var statusClient = authority.GetTestClient();
        statusClient.BaseAddress = new Uri("https://localhost/");
        statusClient.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("reader-app", ["registry-reader"]));
        using var statusResponse = await statusClient.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("catalog", Assert.Single(Assert.IsType<KaleidoRegistryStatus>(status).Services).ServiceName);

        await authority.StopAsync(TestContext.Current.CancellationToken);
        var result = await client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);
        var pinned = await factory.GetClient("catalog", "1").QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);
        var keyed = scope.ServiceProvider.GetRequiredKeyedService<IKaleidoQueryableClient>("catalog");
        var keyedResult = await keyed.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("one", Assert.Single(result.Results).Name);
        Assert.Equal("one", Assert.Single(pinned.Results).Name);
        Assert.Equal("one", Assert.Single(keyedResult.Results).Name);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ForwardedProcessId_UsesTheSameQueryableClientAndDoesNotLeakOutsideTheScope()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new ConcurrentQueue<string?>();
        using var consumer = StartConsumer(authority, publisher, read, request =>
            sent.Enqueue(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values) ? values.SingleOrDefault() : null));
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var scope = consumer.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient("catalog");
        var processId = Guid.NewGuid();
        var forwarding = new KaleidoProcessForwarding();

        using (forwarding.ForwardProcess(processId))
        {
            var result = await client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);
            Assert.Equal("one", Assert.Single(result.Results).Name);
        }

        await client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);
        Assert.Equal([processId.ToString("D"), null], sent.ToArray());
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost StartConsumer(WebApplication authority, WebApplication publisher, TaskCompletionSource<bool> read,
        Action<HttpRequestMessage>? onQuery = null) =>
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
                services.AddKaleido(context.Configuration, new()
                {
                    ServiceName = "reader",
                    ServiceVersion = "1",
                    Assemblies = [typeof(StandaloneQueryableClientFunctionalTests).Assembly],
                    TypeFilter = _ => false,
                })
                    .AddKaleidoClientHandler<ServiceTokenHandler>()
                    .AddRegistryHttpParticipation()
                    .AddQueryableHttpClient();
                services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
                    options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveReadHandler(read)
                    {
                        InnerHandler = authority.GetTestServer().CreateHandler(),
                    }));
                services.Configure<HttpClientFactoryOptions>("kaleido-queryable", options =>
                    options.HttpMessageHandlerBuilderActions.Add(handlers =>
                    {
                        var server = publisher.GetTestServer().CreateHandler();
                        handlers.PrimaryHandler = onQuery is null ? server : new ObserveQueryHandler(onQuery) { InnerHandler = server };
                    }));
            })
            .Build();

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
            Assemblies = [typeof(StandaloneQueryableClientFunctionalTests).Assembly],
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

    private static async Task<WebApplication> StartPublisherAsync(WebApplication authority, TaskCompletionSource<bool> registered)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        });
        builder.Services.AddTestJwtBearer();
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(StandaloneQueryableClientFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(CatalogSource),
        })
            .AddKaleidoClientHandler<ServiceTokenHandler>()
            .AddRegistryHttpParticipation()
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddQueryableHttpBindings();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveRegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapQueryable();
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

    [QuerySource(DisplayName = "Catalog", Description = "Catalog entries")]
    public sealed class CatalogSource : IQuerySource<CatalogRecord>
    {
        public IQueryable<CatalogRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new CatalogRecord { Name = "one" } }.AsQueryable();
    }

    public sealed class CatalogRecord : IQueryContext
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ServiceTokenHandler(KaleidoServiceOptions service) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var roles = service.ServiceName == "catalog"
                ? ["registrant", "registry-reader", "Kaleido.Registrant.catalog"]
                : new[] { "registry-reader" };
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App(service.ServiceName + "-app", roles));
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

    private sealed class ObserveQueryHandler(Action<HttpRequestMessage> onQuery) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onQuery(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
