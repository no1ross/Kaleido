using System.Net;
using System.Net.Http.Json;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Process.Http.Server.FunctionalTests.IdempotencySqlite;

public sealed class SqliteIdempotencyPersistenceFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private const string LogicalKey = "sqlite-logical-call-0a1b2c3d4e5f";

    [Fact]
    public async Task AfterAHostRestart_TheSameKeyReplaysTheExactAnswerWithoutRunningAnotherStep()
    {
        var path = NewDatabase();
        try
        {
            string firstBody;
            await using (var firstHost = await StartAsync(path, "catalog"))
            {
                using var first = await PostAsync(firstHost.GetTestClient(), "catalog", LogicalKey);
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
                firstBody = await first.Content.ReadAsStringAsync(Cancel);
                await firstHost.StopAsync(Cancel);
            }

            await using var restarted = await StartAsync(path, "catalog");
            using var replay = await PostAsync(restarted.GetTestClient(), "catalog", LogicalKey);

            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
            Assert.Equal(firstBody, await replay.Content.ReadAsStringAsync(Cancel));
            Assert.Equal(1, await CountAsync(path, "ProcessContexts"));
            Assert.Equal(1, await CountAsync(path, "IdempotentResults"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TheSameKeyOnAnotherService_InTheSameDatabase_DoesNotReplayTheFirstService()
    {
        var path = NewDatabase();
        try
        {
            await using var catalog = await StartAsync(path, "catalog");
            await using var clinic = await StartAsync(path, "clinic");
            using var first = await PostAsync(catalog.GetTestClient(), "catalog", LogicalKey);
            using var second = await PostAsync(clinic.GetTestClient(), "clinic", LogicalKey);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.False(second.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
            Assert.Equal(2, await CountAsync(path, "ProcessContexts"));
            Assert.Equal(2, await CountAsync(path, "IdempotentResults"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WithoutAKey_EachRequestCreatesItsOwnProcessAndNoStoredAnswer()
    {
        var path = NewDatabase();
        try
        {
            await using var app = await StartAsync(path, "catalog");
            using var first = await PostAsync(app.GetTestClient(), "catalog", key: null);
            using var second = await PostAsync(app.GetTestClient(), "catalog", key: null);
            var original = await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);
            var repeated = await second.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.NotEqual(Assert.IsType<ProcessResult>(original).ProcessId, Assert.IsType<ProcessResult>(repeated).ProcessId);
            Assert.Equal(2, await CountAsync(path, "ProcessContexts"));
            Assert.Equal(0, await CountAsync(path, "IdempotentResults"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ARefusedRequest_ReleasesItsKeyForACorrectedCall()
    {
        var path = NewDatabase();
        try
        {
            await using var app = await StartAsync(path, "catalog");
            using var refused = await PostAsync(app.GetTestClient(), "catalog", LogicalKey, Guid.NewGuid());
            Assert.InRange((int)refused.StatusCode, 400, 499);
            Assert.Equal(0, await CountAsync(path, "IdempotentResults"));

            using var corrected = await PostAsync(app.GetTestClient(), "catalog", LogicalKey);
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
            Assert.False(corrected.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
            Assert.Equal(1, await CountAsync(path, "ProcessContexts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AfterTheStoredAnswerExpires_AKeyRunsAsANewProcess()
    {
        var path = NewDatabase();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        try
        {
            await using var app = await StartAsync(path, "catalog", clock);
            using var first = await PostAsync(app.GetTestClient(), "catalog", LogicalKey);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var original = await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);
            clock.Advance(TimeSpan.FromDays(2));

            using var next = await PostAsync(app.GetTestClient(), "catalog", LogicalKey);
            var repeated = await next.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);

            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            Assert.False(next.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
            Assert.NotEqual(Assert.IsType<ProcessResult>(original).ProcessId, Assert.IsType<ProcessResult>(repeated).ProcessId);
            Assert.Equal(2, await CountAsync(path, "ProcessContexts"));
            Assert.Equal(1, await CountAsync(path, "IdempotentResults"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static string NewDatabase() => Path.Combine(Path.GetTempPath(), $"kaleido-idempotency-{Guid.NewGuid():N}.db");

    private static async Task<WebApplication> StartAsync(string path, string service, TimeProvider? clock = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (clock is not null)
        {
            builder.Services.AddSingleton(clock);
        }

        var connection = $"Data Source={path};Pooling=False";
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = service,
            ServiceVersion = "1",
            Assemblies = [typeof(SqliteIdempotencyPersistenceFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(StartStep) || type == typeof(StartHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer()
            .AddSqliteContextProvider(connection)
            .AddSqliteOutboxProvider(connection)
            .AddSqliteIdempotencyProvider(connection);
        var app = builder.Build();
        app.MapProcessor();
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string service, string? key, Guid? processId = null)
    {
        using (client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/{service}/1/processes/steps/startstep")
            {
                Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
            };
            if (key is not null)
            {
                request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
            }

            if (processId is { } id)
            {
                request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
            }

            return await client.SendAsync(request, Cancel);
        }
    }

    private static async Task<long> CountAsync(string path, string table)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)(await command.ExecuteScalarAsync(Cancel) ?? 0L);
    }

    [ProcessStep(DisplayName = "Start", Description = "Starts a new idempotency process")]
    public sealed record StartStep : IProcessStep;

    public sealed class StartHandler : IProcessStepHandler<StartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
