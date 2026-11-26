using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Http.Client.Transport;
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
    public async Task PublishedClient_RoundTripsTypedRecordsAndViews_AndKeepsServiceErrors()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = StartConsumer(authority, publisher, read);
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        using var scope = consumer.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient("catalog");

        var result = await client.QuerySourceAsync<RichRecord>(nameof(RichSource),
            new QueryRequest(new QueryBody(Page: new QueryPage(10, 0))), TestContext.Current.CancellationToken);
        Assert.Equal(6, result.TotalCount);
        foreach (var (actual, expected) in result.Results.Zip(RichSource.Records))
        {
            Assert.Equal((expected.Id, expected.Code, expected.Name, expected.IsActive, expected.Amount, expected.Status),
                (actual.Id, actual.Code, actual.Name, actual.IsActive, actual.Amount, actual.Status));
            Assert.Equal((expected.EffectiveDate, expected.NullableScore, expected.ExternalId, expected.CreatedAt),
                (actual.EffectiveDate, actual.NullableScore, actual.ExternalId, actual.CreatedAt.ToUniversalTime()));
        }

        var view = await client.QueryViewAsync<RichViewParameters, RichViewRecord>(nameof(RichSource), nameof(RichView),
            new QueryRequest<RichViewParameters>(new RichViewParameters { Category = "Alpha" }), TestContext.Current.CancellationToken);
        Assert.Equal(6, view.TotalCount);
        Assert.Equal([1, 2, 3], view.Results.Select(item => item.Id));

        var invalid = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => client.QuerySourceAsync<RichRecord>(nameof(RichSource),
            new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Region", FilterOperator.Equals, "East"))), TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains(invalid.Errors, error => error.Code == "qry_field_not_filterable");
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
            TypeFilter = type => type == typeof(CatalogSource) || type == typeof(RichSource) || type == typeof(RichView),
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

    public enum RichStatus { Active, Suspended }

    public sealed class RichRecord : IQueryContext
    {
        [Sortable]
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public decimal Amount { get; init; }
        public RichStatus Status { get; init; }
        [Filterable(FilterOperator.Equals)]
        public DateOnly EffectiveDate { get; init; }
        public float? NullableScore { get; init; }
        public Guid ExternalId { get; init; }
        public DateTime CreatedAt { get; init; }
        [Searchable]
        public string Region { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Rich records", Description = "Typed values through the published Queryable client", DefaultSortField = nameof(RichRecord.Id))]
    [Pageable(DefaultSize = 3, MaxSize = 10)]
    public sealed class RichSource : IQuerySource<RichRecord>
    {
        public static IReadOnlyList<RichRecord> Records { get; } =
        [
            new() { Id = 1, Code = "AL-001", Name = "Alpha One", IsActive = true, Amount = 10m, Status = RichStatus.Active, EffectiveDate = new DateOnly(2024, 1, 1), NullableScore = 1.1f, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000001"), CreatedAt = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc), Region = "East" },
            new() { Id = 2, Code = "BE-002", Name = "Beta Two", IsActive = false, Amount = 25m, Status = RichStatus.Suspended, EffectiveDate = new DateOnly(2024, 1, 5), NullableScore = null, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000002"), CreatedAt = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc), Region = "West" },
            new() { Id = 3, Code = "GA-003", Name = "Gamma Three", IsActive = true, Amount = 40m, Status = RichStatus.Active, EffectiveDate = new DateOnly(2024, 2, 1), NullableScore = 3.3f, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000003"), CreatedAt = new DateTime(2024, 1, 3, 9, 30, 0, DateTimeKind.Utc), Region = "South" },
            new() { Id = 4, Code = "AL-004", Name = "Alpha Four", IsActive = true, Amount = 55m, Status = RichStatus.Suspended, EffectiveDate = new DateOnly(2024, 2, 15), NullableScore = 4.4f, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000004"), CreatedAt = new DateTime(2024, 1, 4, 9, 30, 0, DateTimeKind.Utc), Region = "Central" },
            new() { Id = 5, Code = "DE-005", Name = "Delta Five", IsActive = false, Amount = 70m, Status = RichStatus.Suspended, EffectiveDate = new DateOnly(2024, 3, 1), NullableScore = null, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000005"), CreatedAt = new DateTime(2024, 1, 5, 9, 30, 0, DateTimeKind.Utc), Region = "North" },
            new() { Id = 6, Code = "GA-006", Name = "Gamma Six", IsActive = true, Amount = 85m, Status = RichStatus.Active, EffectiveDate = new DateOnly(2024, 3, 20), NullableScore = 6.6f, ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000006"), CreatedAt = new DateTime(2024, 1, 6, 9, 30, 0, DateTimeKind.Utc), Region = "East" },
        ];

        public IQueryable<RichRecord> CreateQuery(QueryExecutionContext executionContext) => Records.Reverse().AsQueryable();
    }

    public sealed class RichViewParameters : IQueryParameters
    {
        [Required]
        [Description("Category for the view")]
        public string Category { get; init; } = string.Empty;
    }

    public sealed record RichViewRecord(int Id, string Code);

    [QueryView(DisplayName = "Rich view", Description = "Projects identifiers and codes", DefaultSortField = nameof(RichRecord.Id))]
    [Pageable(DefaultSize = 3, MaxSize = 5)]
    public sealed class RichView : IQueryViewSource<RichSource, RichRecord, RichViewRecord, RichViewParameters>
    {
        public IQueryable<RichViewRecord> CreateView(IQueryable<RichRecord> query, QueryExecutionContext executionContext) =>
            query.Select(record => new RichViewRecord(record.Id, record.Code));
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
