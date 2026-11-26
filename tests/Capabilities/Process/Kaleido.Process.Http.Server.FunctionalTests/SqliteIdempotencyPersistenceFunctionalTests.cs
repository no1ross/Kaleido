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
    public async Task ARealHandOffAcrossTwoHosts_SharingOneDatabase_KeepsBothProcessRowsAndThePinnedTarget()
    {
        var path = NewDatabase();
        try
        {
            await using var returns = await StartAsync(path, "returns");
            await using var orders = await StartAsync(path, "orders", target: returns);
            using var opened = await PostAsync(orders.GetTestClient(), "orders", key: null);
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            var processId = Assert.IsType<ProcessResult>(await opened.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
            using var ordersClient = orders.GetTestClient();
            using var handoffRequest = new HttpRequestMessage(HttpMethod.Post, "/orders/1/processes/steps/shareddbhandoffstep")
            {
                Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
            };
            handoffRequest.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
            using var handoffResponse = await ordersClient.SendAsync(handoffRequest, Cancel);
            Assert.Equal(HttpStatusCode.OK, handoffResponse.StatusCode);
            var handedOff = Assert.IsType<ProcessResult>(await handoffResponse.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal((ProcessExecutionState.HandOff, "returns", "1"), (handedOff.State, handedOff.TargetProcessorName, handedOff.TargetProcessorVersion));
            using var returnsClient = returns.GetTestClient();
            var targetBefore = Assert.IsType<ProcessStateSnapshot>(await returnsClient.GetFromJsonAsync<ProcessStateSnapshot>($"/returns/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(("returns", processId), (targetBefore.ServiceName, targetBefore.ProcessId));
            Assert.Contains(targetBefore.Steps, step => step.StepName == nameof(StartStep) && step.Status == StepExecutionStatus.Completed);
            using var followupRequest = new HttpRequestMessage(HttpMethod.Post, "/returns/1/processes/steps/followupstep")
            {
                Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
            };
            followupRequest.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
            using var followup = await returnsClient.SendAsync(followupRequest, Cancel);
            Assert.Equal(HttpStatusCode.OK, followup.StatusCode);
            var next = Assert.IsType<ProcessResult>(await followup.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(StepExecutionStatus.Completed, Assert.Single(next.Steps).ExecutionStatus);
            var origin = Assert.IsType<ProcessStateSnapshot>(await ordersClient.GetFromJsonAsync<ProcessStateSnapshot>($"/orders/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
            Assert.Equal((ProcessExecutionState.HandOff, "returns", "1"), (origin.State, origin.TargetProcessorName, origin.TargetProcessorVersion));
            Assert.Equal(2, await CountAsync(path, "ProcessContexts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TwoServicesInTheSameDatabase_CanKeepSeparateRowsUnderOneProcessId()
    {
        var path = NewDatabase();
        try
        {
            await using var orders = await StartAsync(path, "orders");
            await using var returns = await StartAsync(path, "returns");
            using var started = await PostAsync(orders.GetTestClient(), "orders", key: null);
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            var processId = Assert.IsType<ProcessResult>(await started.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
            await using (var scope = returns.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IProcessContextStore>().SaveAsync(new ProcessContext
                {
                    ProcessId = processId,
                    ProcessorName = "returns",
                    ProcessorVersion = "1",
                    State = ProcessExecutionState.Active,
                }, Cancel);
            }

            using var ordersClient = orders.GetTestClient();
            using var returnsClient = returns.GetTestClient();
            var origin = Assert.IsType<ProcessStateSnapshot>(await ordersClient.GetFromJsonAsync<ProcessStateSnapshot>($"/orders/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
            var target = Assert.IsType<ProcessStateSnapshot>(await returnsClient.GetFromJsonAsync<ProcessStateSnapshot>($"/returns/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(("orders", processId), (origin.ServiceName, origin.ProcessId));
            Assert.Equal(("returns", processId), (target.ServiceName, target.ProcessId));
            Assert.Equal(ProcessExecutionState.Active, target.State);
            Assert.Equal(2, await CountAsync(path, "ProcessContexts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TheSameKeyOnAnotherVersion_InTheSameDatabase_RunsThereWithoutReplayingTheFirstVersion()
    {
        var path = NewDatabase();
        try
        {
            await using var first = await StartAsync(path, "catalog");
            await using var second = await StartAsync(path, "catalog", version: "2");
            using var initial = await PostAsync(first.GetTestClient(), "catalog", LogicalKey);
            using var replay = await PostAsync(first.GetTestClient(), "catalog", LogicalKey);
            using var otherVersion = await PostAsync(second.GetTestClient(), "catalog", LogicalKey, version: "2");
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(HttpStatusCode.OK, otherVersion.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
            Assert.False(otherVersion.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
            var firstProcess = Assert.IsType<ProcessResult>(await initial.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
            var otherProcess = Assert.IsType<ProcessResult>(await otherVersion.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
            Assert.NotEqual(firstProcess.ProcessId, otherProcess.ProcessId);
            Assert.Equal(("1", "2"), (firstProcess.ServiceVersion, otherProcess.ServiceVersion));
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
    public async Task AProcessStoredInOneVersion_CannotBeReadOrContinuedByAnotherVersion()
    {
        var path = NewDatabase();
        try
        {
            await using var first = await StartAsync(path, "catalog");
            await using var second = await StartAsync(path, "catalog", version: "2");
            using var started = await PostAsync(first.GetTestClient(), "catalog", key: null);
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            var processId = Assert.IsType<ProcessResult>(await started.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
            using var secondClient = second.GetTestClient();
            using var wrongVersion = await secondClient.GetAsync($"/catalog/2/processes/{processId:D}", Cancel);
            Assert.Equal(HttpStatusCode.NotFound, wrongVersion.StatusCode);
            using var continued = await PostAsync(second.GetTestClient(), "catalog", key: null, processId, version: "2");
            Assert.Equal(HttpStatusCode.BadRequest, continued.StatusCode);
            var error = Assert.IsType<KaleidoErrorResponse>(await continued.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(ProcessorErrorCodes.ProcessNotFound, Assert.Single(error.Errors).Code);
            using var firstClient = first.GetTestClient();
            var state = Assert.IsType<ProcessStateSnapshot>(await firstClient.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(("catalog", "1", processId), (state.ServiceName, state.ServiceVersion, state.ProcessId));
            Assert.Equal(1, await CountAsync(path, "ProcessContexts"));
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

    private static async Task<WebApplication> StartAsync(string path, string service, TimeProvider? clock = null, string version = "1", WebApplication? target = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (clock is not null)
        {
            builder.Services.AddSingleton(clock);
        }
        if (target is not null)
        {
            builder.Services.AddHttpClient("sqlite-target").ConfigurePrimaryHttpMessageHandler(() => target.GetTestServer().CreateHandler());
        }

        var connection = $"Data Source={path};Pooling=False";
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = service,
            ServiceVersion = version,
            Assemblies = [typeof(SqliteIdempotencyPersistenceFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(StartStep) || type == typeof(StartHandler)
                || (service == "returns" && (type == typeof(FollowupStep) || type == typeof(FollowupHandler)))
                || (target is not null && (type == typeof(SharedDbHandoffStep) || type == typeof(SharedDbHandoffHandler))),
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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string service, string? key, Guid? processId = null, string version = "1")
    {
        using (client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/{service}/{version}/processes/steps/startstep")
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

    [ProcessStep(DisplayName = "Follow up", Description = "Continues the target's process")]
    [AvailableAfter<StartStep>]
    public sealed record FollowupStep : IProcessStep;

    public sealed class FollowupHandler : IProcessStepHandler<FollowupStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(FollowupStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Hand off", Description = "Starts the target's process under this id")]
    [AvailableAfter<StartStep>]
    public sealed record SharedDbHandoffStep : IProcessStep;

    public sealed class SharedDbHandoffHandler(IHttpClientFactory clients) : IProcessStepHandler<SharedDbHandoffStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(SharedDbHandoffStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            using var client = clients.CreateClient("sqlite-target");
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/returns/1/processes/steps/startstep")
            {
                Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
            };
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, context.ProcessId.ToString("D"));
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, "orders");
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, cancellationToken)
                ?? throw new InvalidOperationException("The target returned no Process result.");
            return ProcessStepHandlerResult.HandOff("returns", result, nameof(StartStep));
        }
    }
}
