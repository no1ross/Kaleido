using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Registry.Http.Server.FunctionalTests.Sqlite;

public sealed class SqliteRegistryPersistenceFunctionalTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARestartedAuthority_RetainsTheRegistration_AsUnknownUntilTheInstanceHeartbeats()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-registry-{Guid.NewGuid():N}.db");
        var registration = Registration(Guid.NewGuid());
        try
        {
            await using (var first = await StartAsync(path))
            {
                using var client = first.GetTestClient();
                using var response = await client.PostAsJsonAsync("/registry/instances", registration, KaleidoJsonOptions.Options, Cancel);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await TouchWatchAsync(first);
                await first.StopAsync(Cancel);
            }

            time.Advance(TimeSpan.FromMinutes(5));
            await using var restarted = await StartAsync(path);
            await TouchWatchAsync(restarted);
            using var reader = restarted.GetTestClient();
            using var unknownResponse = await reader.GetAsync("/registry/status", Cancel);
            Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
            var unknown = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await unknownResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, Cancel)).Services);
            Assert.Equal(RegistryStatusNames.Unknown, unknown.Status);
            Assert.Equal(1, unknown.UnknownInstances);
            Assert.Equal(RegistryInstanceStateNames.Unknown, Assert.Single(unknown.Instances).State);
            using var registryResponse = await reader.GetAsync("/registry", Cancel);
            var registry = Assert.IsType<KaleidoRegistry>(await registryResponse.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(RegistryStatusNames.Unknown, Assert.Single(registry.Queryables).Status);
            Assert.Equal(HealthStatus.Degraded, await CompletenessAsync(restarted));

            time.Advance(TimeSpan.FromSeconds(5));
            using var heartbeat = await reader.PutAsJsonAsync($"/registry/instances/{registration.InstanceId:D}/heartbeat", new RegistryHeartbeat(), KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
            time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
            using var readyResponse = await reader.GetAsync("/registry/status", Cancel);
            var ready = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await readyResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, Cancel)).Services);
            Assert.Equal(RegistryStatusNames.Available, ready.Status);
            Assert.Equal(RegistryInstanceStateNames.Ready, Assert.Single(ready.Instances).State);
            Assert.Equal(HealthStatus.Healthy, await CompletenessAsync(restarted));
            await restarted.StopAsync(Cancel);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TwoAuthorityNodes_CanShareABuiltViewWithoutReadingTheSecondStore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-registry-{Guid.NewGuid():N}.db");
        var distributed = new ObservingDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var offline = new Mock<IKaleidoRegistryStore>();
        offline.Setup(store => store.ReadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("store read unavailable"));
        offline.Setup(store => store.TouchWatchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(time.GetUtcNow());
        offline.Setup(store => store.AddRegistrationAsync(It.IsAny<RegistryRegistrationRecord>(), It.IsAny<KaleidoRegistry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        offline.Setup(store => store.SaveInstanceAsync(It.IsAny<RegistryInstanceRecord>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        offline.Setup(store => store.GetOrSetCurrentVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, string version, CancellationToken _) => version);
        try
        {
            await using var first = await StartAsync(path, distributed);
            await using var second = await StartAsync(path, distributed, offline.Object);
            var registration = Registration(Guid.NewGuid());
            using var firstClient = first.GetTestClient();
            using var secondClient = second.GetTestClient();
            using var registered = await firstClient.PostAsJsonAsync("/registry/instances", registration, KaleidoJsonOptions.Options, Cancel);
            using var aligned = await secondClient.PostAsJsonAsync("/registry/instances", registration, KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            Assert.Equal(HttpStatusCode.OK, aligned.StatusCode);

            using var primed = await firstClient.GetAsync("/registry", Cancel);
            Assert.Equal(HttpStatusCode.OK, primed.StatusCode);
            var source = Assert.IsType<KaleidoRegistry>(await primed.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
            await distributed.ViewWritten.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
            Assert.NotEmpty(distributed.WrittenKeys);
            using var remote = await secondClient.GetAsync("/registry", Cancel);
            Assert.Equal(HttpStatusCode.OK, remote.StatusCode);
            var shared = Assert.IsType<KaleidoRegistry>(await remote.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal("orders", Assert.Single(shared.Queryables).Name);
            Assert.Equal(source.Revision, shared.Revision);
            Assert.Equal(source.GeneratedAt, shared.GeneratedAt);
            Assert.Contains(distributed.ReadKeys, key => distributed.WrittenKeys.Contains(key));
            offline.Verify(store => store.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
            await second.StopAsync(Cancel);
            await first.StopAsync(Cancel);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnavailableDistributedCache_DoesNotPreventARegistryReadAfterViewExpiration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-registry-{Guid.NewGuid():N}.db");
        try
        {
            await using var app = await StartAsync(path, new UnavailableDistributedCache());
            using var client = app.GetTestClient();
            using var registered = await client.PostAsJsonAsync("/registry/instances", Registration(Guid.NewGuid()), KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            using var firstResponse = await client.GetAsync("/registry", Cancel);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            var first = Assert.IsType<KaleidoRegistry>(await firstResponse.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
            time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
            using var afterExpiry = await client.GetAsync("/registry", Cancel);
            Assert.Equal(HttpStatusCode.OK, afterExpiry.StatusCode);
            var rebuilt = Assert.IsType<KaleidoRegistry>(await afterExpiry.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal("orders", Assert.Single(rebuilt.Queryables).Name);
            Assert.Equal(first.Revision, rebuilt.Revision);
            Assert.NotEqual(first.GeneratedAt, rebuilt.GeneratedAt);
            await app.StopAsync(Cancel);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static RegistryInstanceRegistration Registration(Guid instanceId) => new()
    {
        ServiceName = "catalog",
        ServiceVersion = "1",
        BuildVersion = "1.0.0-preview.616",
        InstanceId = instanceId,
        Queryables = [new QueryableRegistryItem
        {
            ServiceName = "catalog",
            Name = "orders",
            Bindings = new RegistryTransportBindings
            {
                Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/orders/query" }],
            },
        }],
        Transports = [new RegistryTransportRoute { Transport = "http", GatewayRoute = "/catalog/1" }],
    };

    private async Task<WebApplication> StartAsync(string path, IDistributedCache? distributed = null, IKaleidoRegistryStore? overrideStore = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(time);
        if (distributed is not null)
        {
            builder.Services.AddSingleton(distributed);
        }
        builder.Services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryClock(time));
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(SqliteRegistryPersistenceFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry()
            .AddSqliteRegistryProvider($"Data Source={path};Pooling=False")
            .AddRegistryHttpServer();
        if (overrideStore is not null)
        {
            builder.Services.Replace(ServiceDescriptor.Singleton(overrideStore));
        }

        var app = builder.Build();
        app.MapKaleidoControlRegistry();
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoRegistryDbContext>().Database.EnsureCreatedAsync(Cancel);
        await app.StartAsync(Cancel);
        return app;
    }

    private async Task TouchWatchAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>()
            .TouchWatchAsync(time.GetUtcNow(), TimeSpan.FromSeconds(30), Cancel);
    }

    private static async Task<HealthStatus> CompletenessAsync(WebApplication app) =>
        (await app.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Name == "kaleido-registry-completeness", Cancel)).Status;

    private sealed class ObservingDistributedCache(MemoryDistributedCache inner) : IDistributedCache
    {
        private readonly TaskCompletionSource viewWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> WrittenKeys { get; } = new();
        public ConcurrentQueue<string> ReadKeys { get; } = new();
        public Task ViewWritten => viewWritten.Task;
        public byte[]? Get(string key)
        {
            ReadKeys.Enqueue(key);
            return inner.Get(key);
        }
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            ReadKeys.Enqueue(key);
            return inner.GetAsync(key, token);
        }
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            inner.Set(key, value, options);
            RecordWrite(key);
        }
        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            await inner.SetAsync(key, value, options, token);
            RecordWrite(key);
        }
        private void RecordWrite(string key)
        {
            WrittenKeys.Enqueue(key);
            if (key.StartsWith("kaleido:registry:view", StringComparison.Ordinal))
            {
                viewWritten.TrySetResult();
            }
        }
    }

    private sealed class UnavailableDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new IOException("distributed cache unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(new IOException("distributed cache unavailable"));
        public void Refresh(string key) => throw new IOException("distributed cache unavailable");
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("distributed cache unavailable"));
        public void Remove(string key) => throw new IOException("distributed cache unavailable");
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("distributed cache unavailable"));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new IOException("distributed cache unavailable");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromException(new IOException("distributed cache unavailable"));
    }

    private sealed class RegistryClock(FakeTimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
}
