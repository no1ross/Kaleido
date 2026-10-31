using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Processor;
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

        using var first = await PostAsync(client, "key-1-0a1b2c3d4e5f");
        using var retry = await PostAsync(client, "key-1-0a1b2c3d4e5f");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.False(first.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal("true", Assert.Single(retry.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));

        var firstBody = await first.Content.ReadAsStringAsync(Cancel);
        Assert.Equal(firstBody, await retry.Content.ReadAsStringAsync(Cancel));

        // One process (the step ran once) and one stored answer, keyed per service and caller.
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
        Assert.Equal(KaleidoIdempotencyEndpointFilter.RecordKey("kaleido", "anonymous", string.Empty, "key-1-0a1b2c3d4e5f"), await ScalarAsync("SELECT Key FROM IdempotentResults"));
        Assert.Equal(ProcessIdOf(firstBody), ProcessIdOf((string)(await ScalarAsync("SELECT Response FROM IdempotentResults"))!));

        // #514: the replay of a new process's answer carries its id in the header too, as the first did.
        Assert.Equal(ProcessIdOf(firstBody), Assert.Single(retry.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        Assert.Equal(first.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId), retry.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId));
    }

    [Fact]
    public async Task ARetryAfterARestart_StillGetsTheStoredAnswerFromTheDatabase()
    {
        string firstBody;

        using (var host = await StartHostAsync())
        {
            using var first = await PostAsync(host.GetTestClient(), "key-restart-0a1b2c3d4e5f");
            firstBody = await first.Content.ReadAsStringAsync(Cancel);
            await host.StopAsync(Cancel);
        }

        using var restarted = await StartHostAsync();
        using var retry = await PostAsync(restarted.GetTestClient(), "key-restart-0a1b2c3d4e5f");

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

        using var first = await PostAsync(client, "key-reused-0a1b2c3d4e5f");
        using var different = await PostAsync(client, "key-reused-0a1b2c3d4e5f", processId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
        Assert.Contains("pro_idempotency_key_reused", await different.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    [Fact]
    public async Task TheSameKeyFromAnotherService_IsAnotherKey()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-scope-0a1b2c3d4e5f");

        await ExecuteAsync($"UPDATE IdempotentResults SET Key = '{KaleidoIdempotencyEndpointFilter.RecordKey("other-service", "anonymous", string.Empty, "key-scope-0a1b2c3d4e5f")}'");

        using var second = await PostAsync(client, "key-scope-0a1b2c3d4e5f");

        Assert.False(second.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    [Fact]
    public async Task AnExpiredAnswer_IsNotReturned_AndIsDeletedWhenTheNextAnswerIsStored()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var first = await PostAsync(client, "key-expired-0a1b2c3d4e5f");

        await ExecuteAsync($"UPDATE IdempotentResults SET ExpiresUtc = {DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks}");

        using var retry = await PostAsync(client, "key-expired-0a1b2c3d4e5f");

        Assert.False(retry.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM IdempotentResults"));
    }

    // #441: a key whose attempt may have run (marked unknown, or still in progress past its lease) is
    // refused until it expires - here dated past the replay window but inside the unknown window - and
    // an operator resolves it by deleting the record, after which the key runs again.
    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    public async Task AKeyWhoseOutcomeIsUnknown_IsRefused_UntilAnOperatorDeletesTheRecord(int state, int leaseMinutes)
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        using var first = await PostAsync(client, "key-unknown-0a1b2c3d4e5f");
        await ExecuteAsync(
            $"UPDATE IdempotentResults SET State = {state}, Response = '', StatusCode = 0, LeaseUntilUtc = {DateTimeOffset.UtcNow.AddMinutes(leaseMinutes).UtcTicks}, ExpiresUtc = {DateTimeOffset.UtcNow.AddDays(3).UtcTicks}");

        using var refused = await PostAsync(client, "key-unknown-0a1b2c3d4e5f");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("pro_idempotency_outcome_unknown", await refused.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));

        await ExecuteAsync("DELETE FROM IdempotentResults");
        using var resolved = await PostAsync(client, "key-unknown-0a1b2c3d4e5f");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ProcessContexts"));
    }

    // #441: a 4xx is a refusal before anything ran: the key is released (no record) and may be retried.
    [Fact]
    public async Task ARefusedRequest_ReleasesItsKey()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        using var refused = await PostAsync(client, "key-refused-0a1b2c3d4e5f", processId: Guid.NewGuid());

        Assert.InRange((int)refused.StatusCode, 400, 499);
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM IdempotentResults"));
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
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = FunctionalProcessorNames.TestProcessor,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(RuntimeRootStep).Assembly],
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
