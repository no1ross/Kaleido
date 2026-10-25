using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

// End to end against the SQLite reference store (a real database file): the answer to a request with
// an Idempotency-Key is stored in IdempotentResults and read back for a retry, so the step does not run
// again. RuntimeRootStep starts a new process on every run, so "ran again" shows as a second process.
public sealed class IdempotentExecutionTests : IAsyncLifetime
{
    private const string RootStepUrl = "/kaleido/processes/steps/runtimerootstep";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-idempotency-{Guid.NewGuid():N}.db");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ARetryWithTheSameKey_GetsTheStoredAnswer_AndTheStepDoesNotRunAgain()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-1");
        using var retry = await PostAsync(client, "key-1");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.False(first.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal("true", Assert.Single(retry.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));

        var firstBody = await first.Content.ReadAsStringAsync(Cancel);
        Assert.Equal(firstBody, await retry.Content.ReadAsStringAsync(Cancel));

        // One process (the step ran once) and one stored answer, keyed per service and caller.
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
        Assert.Equal("kaleido:anonymous:key-1", await ScalarAsync("SELECT Key FROM IdempotentResults"));
        Assert.Equal(ProcessIdOf(firstBody), ProcessIdOf((string)(await ScalarAsync("SELECT Response FROM IdempotentResults"))!));
    }

    [Fact]
    public async Task ARetryAfterARestart_StillGetsTheStoredAnswerFromTheDatabase()
    {
        string firstBody;

        using (var host = await StartHostAsync())
        {
            using var first = await PostAsync(host.GetTestClient(), "key-restart");
            firstBody = await first.Content.ReadAsStringAsync(Cancel);
            await host.StopAsync(Cancel);
        }

        using var restarted = await StartHostAsync();
        using var retry = await PostAsync(restarted.GetTestClient(), "key-restart");

        Assert.Equal("true", Assert.Single(retry.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
        Assert.Equal(firstBody, await retry.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    [Fact]
    public async Task WithoutAKey_EveryRequestRuns()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, key: null);
        using var second = await PostAsync(client, key: null);

        Assert.NotEqual(ProcessIdOf(await first.Content.ReadAsStringAsync(Cancel)), ProcessIdOf(await second.Content.ReadAsStringAsync(Cancel)));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM IdempotentResults"));
    }

    [Fact]
    public async Task TheSameKeyWithADifferentRequest_IsRefused_AndNothingRuns()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-reused");
        using var different = await PostAsync(client, "key-reused", processId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
        Assert.Contains("pro_idempotency_key_reused", await different.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    [Fact]
    public async Task TheSameKeyFromAnotherService_IsAnotherKey()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-scope");

        await ExecuteAsync("UPDATE IdempotentResults SET Key = 'other-service:anonymous:key-scope'");

        using var second = await PostAsync(client, "key-scope");

        Assert.False(second.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    [Fact]
    public async Task AnExpiredAnswer_IsNotReturned_AndIsDeletedWhenTheNextAnswerIsStored()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-expired");

        await ExecuteAsync($"UPDATE IdempotentResults SET ExpiresUtc = {DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks}");

        using var retry = await PostAsync(client, "key-expired");

        Assert.False(retry.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM IdempotentResults"));
    }

    // Like the PriorAuth services: the host creates the store's schema at startup (the SQLite store does
    // not do that itself).
    private async Task<IHost> StartHostAsync()
    {
        var host = await BuildHostAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);

        return host;
    }

    private async Task<IHost> BuildHostAsync() =>
        await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = FunctionalProcessorNames.TestProcessor;
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            o.Assemblies = [typeof(RuntimeRootStep).Assembly];
                        })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddSqliteContextStore($"Data Source={_databasePath}").AddSqliteOutboxStore($"Data Source={_databasePath}").AddSqliteIdempotencyStore($"Data Source={_databasePath}");
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string? key, Guid? processId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RootStepUrl)
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json"),
        };

        if (key is not null)
        {
            request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        }

        if (processId is not null)
        {
            request.Headers.Add(KaleidoCorrelationHeaders.ProcessId, processId.Value.ToString());
        }

        return await client.SendAsync(request, Cancel);
    }

    private static string? ProcessIdOf(string json) =>
        JsonNode.Parse(json)?["processId"]?.GetValue<string>();

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Cancel);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Cancel);
    }
}
