using System.Net;
using System.Net.Http.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Registry.IntegrationTests;

public sealed class RegistryPublicationIntegrationTests
{
    [Fact]
    public async Task QueryableHost_PublishesItsActualMappedHttpBinding_ThenDeregisters()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var response = await authority.GetTestClient().GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registry = await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var source = Assert.Single(Assert.IsType<KaleidoRegistry>(registry).Queryables);
        Assert.Equal("CatalogSource", source.Name);
        Assert.Equal("Catalog entries", source.Description);
        Assert.Equal("/catalog/1/queryable/catalogsource/query", Assert.Single(source.Bindings.Http).Path);
        var name = Assert.Single(source.Fields, field => field.Name == nameof(CatalogRecord.Name));
        Assert.Equal("The record's display name", name.Description);
        Assert.Equal(["Catalog:Personal"], name.Classifications);
        Assert.True(name.IsSearchable);
        var amount = Assert.Single(source.Fields, field => field.Name == nameof(CatalogRecord.Amount));
        Assert.Equal(["equals", "greaterThanOrEqual", "lessThan", "between"], amount.FilterOperators);
        Assert.True(amount.IsSortable);
        var region = Assert.Single(source.Fields, field => field.Name == nameof(CatalogRecord.Region));
        Assert.False(region.IsFilterable);
        Assert.False(region.IsSortable);
        Assert.True(region.IsSearchable);
        Assert.Equal(3, source.Pageable?.DefaultSize);
        Assert.Equal(10, source.Pageable?.MaxSize);
        var view = Assert.Single(source.Views);
        Assert.Equal("Catalog view", view.DisplayName);
        Assert.Equal("/catalog/1/queryable/catalogsource/catalogview/query", Assert.Single(view.Bindings.Http).Path);
        Assert.Single(view.Parameters);
        Assert.NotEmpty(view.OutputFields);
        Assert.Equal(2, view.Pageable?.DefaultSize);
        Assert.Equal(4, view.Pageable?.MaxSize);

        using var mapped = await publisher.GetTestClient().GetAsync("/catalog/1/queryable/catalogsource/query", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, mapped.StatusCode);
        await publisher.StopAsync(TestContext.Current.CancellationToken);

        using var statusResponse = await authority.GetTestClient().GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await statusResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal("catalog", status.ServiceName);
        Assert.Empty(status.Instances);
        using var availabilityResponse = await authority.GetTestClient().GetAsync("/registry/availability", TestContext.Current.CancellationToken);
        var availability = Assert.Single(Assert.IsType<KaleidoRegistryAvailability>(await availabilityResponse.Content.ReadFromJsonAsync<KaleidoRegistryAvailability>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, availability.Status);
    }

    [Fact]
    public async Task AnUnhealthyPublisher_ReportsNotReadyOnItsNextAutonomousHeartbeat()
    {
        var time = new HeartbeatClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        await using var authority = await StartAuthorityAsync(time);
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeat = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var health = new LocalHealth { Healthy = false };
        await using var publisher = await StartPublisherAsync(authority, registered, time, health, heartbeat, refreshed, readsDrained);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(publisher);
        await time.HeartbeatScheduled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var client = authority.GetTestClient();
        using var before = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var available = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await before.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Available, available.Status);

        for (var seconds = 0; seconds < 12 && !heartbeat.Task.IsCompleted; seconds++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await heartbeat.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await readsDrained.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        using var after = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var unavailable = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await after.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, unavailable.Status);
        Assert.Equal(RegistryInstanceStateNames.NotReady, Assert.Single(unavailable.Instances).State);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASecondParticipant_HydratesUnavailableAfterAHeartbeatOrStop(bool stopPublisher)
    {
        var time = new HeartbeatClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        await using var authority = await StartAuthorityAsync(time);
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeat = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var health = new LocalHealth();
        await using var publisher = await StartPublisherAsync(authority, registered, time, health, heartbeat, refreshed, readsDrained);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(publisher);
        await time.HeartbeatScheduled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stored = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? expectedEtag = null;
        var readerClock = new ReaderClock(time.GetUtcNow());
        await using var consumer = BuildReader(authority, publisher, read, queryableClientFirst: false, readerClock,
            () => expectedEtag, updated, stored);
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        await readerClock.FirstCheckScheduled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var client = authority.GetTestClient();
        if (stopPublisher)
        {
            await publisher.StopAsync(TestContext.Current.CancellationToken);
            using var status = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
            var service = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await status.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
            Assert.Empty(service.Instances);
        }
        else
        {
            health.Healthy = false;
            for (var seconds = 0; seconds < 12 && !heartbeat.Task.IsCompleted; seconds++)
            {
                time.Advance(TimeSpan.FromSeconds(1));
                await Task.Yield();
            }

            await heartbeat.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await readsDrained.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        }

        using var availability = await client.GetAsync("/registry/availability", TestContext.Current.CancellationToken);
        var serviceAvailability = Assert.Single(Assert.IsType<KaleidoRegistryAvailability>(await availability.Content.ReadFromJsonAsync<KaleidoRegistryAvailability>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, serviceAvailability.Status);
        expectedEtag = availability.Headers.ETag?.Tag;
        Assert.NotNull(expectedEtag);
        readerClock.Advance(TimeSpan.FromSeconds(10));
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await readerClock.SecondCheckScheduled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        readerClock.Advance(TimeSpan.FromSeconds(10));
        await stored.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        if (!stopPublisher)
        {
            await publisher.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HydratedReader_QueriesThePublishedQueryableHttpBinding(bool queryableClientFirst)
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = await StartPublisherAsync(authority, registered);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = BuildReader(authority, publisher, read, queryableClientFirst);
        using var scope = consumer.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient("catalog");
        var unavailable = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() =>
            client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken));
        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, unavailable.Code);

        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(consumer);
        var result = await client.QuerySourceAsync<CatalogRecord>("CatalogSource", new QueryRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("one", Assert.Single(result.Results).Name);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WebApplication BuildReader(WebApplication authority, WebApplication publisher, TaskCompletionSource<bool> read,
        bool queryableClientFirst, FakeTimeProvider? time = null, Func<string?>? expectedEtag = null,
        TaskCompletionSource<bool>? updated = null, TaskCompletionSource<bool>? stored = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (time is not null)
        {
            builder.Services.AddSingleton<TimeProvider>(time);
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://localhost/",
        });
        var kaleido = builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryPublicationIntegrationTests).Assembly],
            TypeFilter = _ => false,
        });
        if (queryableClientFirst)
        {
            kaleido.AddQueryableHttpClient().AddRegistryHttpParticipation();
        }
        else
        {
            kaleido.AddRegistryHttpParticipation().AddQueryableHttpClient();
        }
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveReadHandler(read, expectedEtag, updated, stored)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-queryable", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = publisher.GetTestServer().CreateHandler()));
        return builder.Build();
    }

    private static async Task WaitUntilReadyAsync(WebApplication consumer)
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

    private static async Task<WebApplication> StartPublisherAsync(WebApplication authority, TaskCompletionSource<bool> registered,
        FakeTimeProvider? time = null, LocalHealth? health = null, TaskCompletionSource<bool>? heartbeat = null, TaskCompletionSource<bool>? refreshed = null,
        TaskCompletionSource<bool>? readsDrained = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (time is not null)
        {
            builder.Services.AddSingleton<TimeProvider>(time);
        }

        if (health is not null)
        {
            builder.Services.AddHealthChecks().AddCheck("local", () => health.Healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        }
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://localhost/",
        });
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryPublicationIntegrationTests).Assembly],
            TypeFilter = type => type == typeof(CatalogSource) || type == typeof(CatalogView),
        })
            .AddRegistryHttpParticipation()
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddQueryableHttpBindings();
        builder.Services.Configure<Microsoft.Extensions.Http.HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveRegistrationHandler(registered, heartbeat, refreshed, readsDrained)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        var app = builder.Build();
        app.MapQueryable();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartAuthorityAsync(FakeTimeProvider? time = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (time is not null)
        {
            builder.Services.AddSingleton<TimeProvider>(time);
            builder.Services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryClock(time));
        }

        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryPublicationIntegrationTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { Cache = new KaleidoRegistryCacheOptions { ViewTtl = time is null ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(1) } })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.MapKaleidoControlRegistry();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    [QuerySource(DisplayName = "Catalog", Description = "Catalog entries", DefaultSortField = nameof(CatalogRecord.Amount))]
    [Pageable(DefaultSize = 3, MaxSize = 10)]
    public sealed class CatalogSource : IQuerySource<CatalogRecord>
    {
        public IQueryable<CatalogRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new CatalogRecord { Name = "one" } }.AsQueryable();
    }

    private static readonly DataClassification Personal = new("Catalog", "Personal");

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class PersonalAttribute() : DataClassificationAttribute(Personal);

    public sealed class CatalogRecord : IQueryContext
    {
        [Personal]
        [Description("The record's display name")]
        [Searchable]
        [Sortable]
        public string Name { get; init; } = string.Empty;

        [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.Between)]
        [Sortable]
        public decimal Amount { get; init; }

        [Searchable]
        public string Region { get; init; } = string.Empty;
    }

    public sealed class CatalogViewParameters : IQueryParameters
    {
        [Required]
        [Description("Category for the catalog view")]
        public string Category { get; init; } = string.Empty;
    }

    public sealed class CatalogViewRecord
    {
        public string Name { get; init; } = string.Empty;
    }

    [QueryView(DisplayName = "Catalog view", Description = "Catalog names with a category parameter", DefaultSortField = nameof(CatalogRecord.Name))]
    [Pageable(DefaultSize = 2, MaxSize = 4)]
    public sealed class CatalogView : IQueryViewSource<CatalogSource, CatalogRecord, CatalogViewRecord, CatalogViewParameters>
    {
        public IQueryable<CatalogViewRecord> CreateView(IQueryable<CatalogRecord> query, QueryExecutionContext executionContext) =>
            query.Select(record => new CatalogViewRecord { Name = record.Name });
    }

    private sealed class ReaderClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private readonly TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int checks;
        public Task FirstCheckScheduled => first.Task;
        public Task SecondCheckScheduled => second.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (period == Timeout.InfiniteTimeSpan && dueTime == TimeSpan.FromSeconds(10))
            {
                if (Interlocked.Increment(ref checks) == 1)
                {
                    first.TrySetResult();
                }
                else
                {
                    second.TrySetResult();
                }
            }

            return timer;
        }
    }

    private sealed class HeartbeatClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private readonly TaskCompletionSource scheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task HeartbeatScheduled => scheduled.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (period == Timeout.InfiniteTimeSpan && dueTime >= TimeSpan.FromSeconds(9)
                && dueTime <= TimeSpan.FromSeconds(11) && dueTime != TimeSpan.FromSeconds(10))
            {
                scheduled.TrySetResult();
            }

            return timer;
        }
    }

    private sealed class LocalHealth
    {
        public bool Healthy { get; set; } = true;
    }

    private sealed class RegistryClock(FakeTimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }

    private sealed class ObserveReadHandler(TaskCompletionSource<bool> observed, Func<string?>? expectedEtag,
        TaskCompletionSource<bool>? updated, TaskCompletionSource<bool>? stored) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Get
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/capabilities", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                observed.TrySetResult(true);
            }

            if (request.Method == HttpMethod.Get
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/availability", StringComparison.Ordinal)
                && expectedEtag?.Invoke() is { } expected)
            {
                if (response.IsSuccessStatusCode && response.Headers.ETag?.Tag == expected)
                {
                    updated?.TrySetResult(true);
                }

                if (request.Headers.IfNoneMatch.Any(tag => tag.Tag == expected))
                {
                    stored?.TrySetResult(true);
                }
            }

            return response;
        }
    }

    private sealed class ObserveRegistrationHandler(TaskCompletionSource<bool> observed, TaskCompletionSource<bool>? heartbeat,
        TaskCompletionSource<bool>? refreshed, TaskCompletionSource<bool>? readsDrained) : DelegatingHandler
    {
        private int inFlightReads;
        private int heartbeatSeen;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var documentRead = readsDrained is not null && request.Method == HttpMethod.Get
                && (request.RequestUri?.AbsolutePath is "/registry/availability" or "/registry/capabilities");
            if (documentRead)
            {
                Interlocked.Increment(ref inFlightReads);
            }

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (request.Method == HttpMethod.Post
                    && string.Equals(request.RequestUri?.AbsolutePath, "/registry/instances", StringComparison.Ordinal)
                    && response.IsSuccessStatusCode)
                {
                    observed.TrySetResult(true);
                }

                if (request.Method == HttpMethod.Get
                    && string.Equals(request.RequestUri?.AbsolutePath, "/registry/capabilities", StringComparison.Ordinal)
                    && response.IsSuccessStatusCode)
                {
                    refreshed?.TrySetResult(true);
                }

                if (request.Method == HttpMethod.Put
                    && request.RequestUri?.AbsolutePath.EndsWith("/heartbeat", StringComparison.Ordinal) == true
                    && response.IsSuccessStatusCode)
                {
                    Volatile.Write(ref heartbeatSeen, 1);
                    if (Volatile.Read(ref inFlightReads) == 0)
                    {
                        readsDrained?.TrySetResult(true);
                    }

                    heartbeat?.TrySetResult(true);
                }

                return response;
            }
            finally
            {
                if (documentRead && Interlocked.Decrement(ref inFlightReads) == 0 && Volatile.Read(ref heartbeatSeen) == 1)
                {
                    readsDrained?.TrySetResult(true);
                }
            }
        }
    }
}
