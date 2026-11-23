using System.Net;
using System.Net.Http.Json;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
        var registration = new RegistryInstanceRegistration
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            BuildVersion = "1.0.0-preview.616",
            InstanceId = Guid.NewGuid(),
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
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private async Task<WebApplication> StartAsync(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(time);
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

    private sealed class RegistryClock(FakeTimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
}
