using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Testing.Identity;
using Kaleido.Process.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests;

public sealed class ProcessHttpServerFunctionalTests
{
    private const string StepUrl = "/catalog/1/processes/steps/revieworderstep";

    [Fact]
    public async Task LocalProcess_ExecutesAndReadsNeutralStateWithoutARegistryParticipant()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var requestId = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestId);
        using var execute = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var result = await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var process = Assert.IsType<ProcessResult>(result);
        Assert.Equal("1", process.ServiceVersion);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(process.Steps).ExecutionStatus);
        Assert.Equal(process.ProcessId.ToString(), execute.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).Single());
        Assert.Equal(requestId, execute.Headers.GetValues(KaleidoCorrelationHeaders.RequestId).Single());

        using var stateResponse = await client.GetAsync($"/catalog/1/processes/{process.ProcessId:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var snapshot = await stateResponse.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal((process.ProcessId, "catalog", "1"), (Assert.IsType<ProcessStateSnapshot>(snapshot).ProcessId, snapshot.ServiceName, snapshot.ServiceVersion));
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(snapshot.Steps, step => step.StepName == nameof(ReviewOrderStep)).Status);
        Assert.DoesNotContain("executeUrl", await stateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task ContinuationNames_AreFilteredForTheCallerOnExecuteAndState()
    {
        await using var app = await StartAsync();
        using var reviewer = Client(app);
        using var execute = await reviewer.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var result = Assert.IsType<ProcessResult>(await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Empty(result.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, result.Notice?.Code);

        var reviewerState = await reviewer.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{result.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var filtered = Assert.IsType<ProcessStateSnapshot>(reviewerState);
        Assert.Empty(filtered.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, filtered.Notice?.Code);

        using var admin = Client(app, roles: ["reviewer", "admin"]);
        var adminState = await admin.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{result.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var visible = Assert.IsType<ProcessStateSnapshot>(adminState);
        Assert.Equal([nameof(AdminOnlyStep)], visible.AvailableSteps);
        Assert.Null(visible.Notice);
    }

    [Fact]
    public async Task HandOff_ReturnsAndPersistsTheTargetVersionWithoutResolvingAUrl()
    {
        await using var app = await StartAsync(handOff: true);
        using var client = Client(app);
        using var execute = await client.PostAsJsonAsync("/catalog/1/processes/steps/handoffstep",
            new ExecuteStepRequest<HandOffStep> { ProcessStep = new HandOffStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var response = Assert.IsType<ProcessResult>(await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("1", "target", "2"), (response.ServiceVersion, response.TargetProcessorName, response.TargetProcessorVersion));
        Assert.Equal(["FinishStep"], response.AvailableSteps);
        Assert.DoesNotContain("executeUrl", await execute.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);

        using var stateResponse = await client.GetAsync($"/catalog/1/processes/{response.ProcessId:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var snapshot = Assert.IsType<ProcessStateSnapshot>(await stateResponse.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("1", "target", "2"), (snapshot.ServiceVersion, snapshot.TargetProcessorName, snapshot.TargetProcessorVersion));
    }

    [Fact]
    public async Task MultiStepExecute_ReplaysAKeyWithoutRunningTheStepAgain_AndRefusesAChangedBody()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var first = await SendAsync(client, "A-1", "same-operation");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var replay = await SendAsync(client, "A-1", "same-operation");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed).Single());
        var repeated = await replay.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(Assert.IsType<ProcessResult>(original).ProcessId, Assert.IsType<ProcessResult>(repeated).ProcessId);
        Assert.Equal(original.ProcessId.ToString(), replay.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).Single());
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);

        using var changed = await SendAsync(client, "A-2", "same-operation");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        var error = await changed.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.IdempotencyKeyReused, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task ACorrectedKeyedRequest_CanRunAfterTheFirstRequestWasRefused()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/execute")
        {
            Content = JsonContent.Create(new ExecuteProcessRequest
            {
                Steps = [new ProcessStepRequest { StepName = "NoSuchStep", Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options) }],
            }, options: KaleidoJsonOptions.Options),
        };
        invalid.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, "correct-after-refusal");
        using var refused = await client.SendAsync(invalid, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        using var corrected = await SendAsync(client, "A-1", "correct-after-refusal");
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.False(corrected.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task LocalProcess_EnforcesTheStepRoleAndLeavesUnrelatedHostRoutesAlone()
    {
        await using var app = await StartAsync();
        using var client = Client(app, roles: []);
        using var forbidden = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var multi = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ReviewOrderStep),
                Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-1" }, KaleidoJsonOptions.Options),
            }],
        };
        using var deniedMulti = await client.PostAsJsonAsync("/catalog/1/processes/execute", multi, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedMulti.StatusCode);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);

        using var ordinary = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
        Assert.False(ordinary.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Fact]
    public async Task ConcurrentStateChange_AnswersAProcessConflictWithoutRunningTheStep()
    {
        await using var app = await StartAsync();
        app.Services.GetRequiredService<StoredProcesses>().RefuseNextSave();
        using var client = Client(app);

        using var response = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.ProcessConflict, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task InvalidStepInput_IsRejectedBeforeCreatingAProcess()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, StepUrl)
        {
            Content = new StringContent("""{"processStep":{}}""", System.Text.Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.NotEmpty(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);
        Assert.Empty(app.Services.GetRequiredService<StoredProcesses>().States);
    }

    [Fact]
    public async Task UnknownStep_IsRejectedBeforeCreatingAProcess()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var request = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = "NoSuchStep", Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options) }],
        };

        using var response = await client.PostAsJsonAsync("/catalog/1/processes/execute", request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Empty(app.Services.GetRequiredService<StoredProcesses>().States);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string code, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/execute")
        {
            Content = JsonContent.Create(new ExecuteProcessRequest
            {
                Steps = [new ProcessStepRequest
                {
                    StepName = nameof(ReviewOrderStep),
                    Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = code }, KaleidoJsonOptions.Options),
                }],
            }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpClient Client(WebApplication app, string[]? roles = null)
    {
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", roles ?? ["reviewer"]));
        return client;
    }

    private static async Task<WebApplication> StartAsync(bool handOff = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddSingleton<ReviewRuns>();
        builder.Services.AddSingleton(new StoredProcesses());
        builder.Services.AddSingleton(new StoredIdempotency());
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(ProcessHttpServerFunctionalTests).Assembly],
            TypeFilter = type => handOff
                ? type == typeof(HandOffStep) || type == typeof(HandOffHandler)
                : type == typeof(ReviewOrderStep) || type == typeof(ReviewOrderHandler)
                    || type == typeof(AdminOnlyStep) || type == typeof(AdminOnlyHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddContextStore<TestProcessContextStore>()
            .AddIdempotencyStore<TestIdempotencyStore>()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpServer();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProcessor();
        app.MapGet("/ordinary", () => Results.Text("host"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public sealed class ReviewRuns
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Increment() => Interlocked.Increment(ref _count);
    }

    [ProcessStep(DisplayName = "Review order", Description = "Reviews an order")]
    [KaleidoAuthorization(Roles = "reviewer")]
    public sealed record ReviewOrderStep : IProcessStep
    {
        public required string Code { get; init; }
    }

    public sealed class ReviewOrderHandler(ReviewRuns runs) : IProcessStepHandler<ReviewOrderStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(ReviewOrderStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            runs.Increment();
            return Task.FromResult(ProcessStepHandlerResult.Success());
        }
    }

    [ProcessStep(DisplayName = "Admin review", Description = "Reviews the order after its first step")]
    [AvailableAfter<ReviewOrderStep>]
    [KaleidoAuthorization(Roles = "admin")]
    public sealed record AdminOnlyStep : IProcessStep;

    public sealed class AdminOnlyHandler : IProcessStepHandler<AdminOnlyStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(AdminOnlyStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Hand off", Description = "Hands a process to a versioned target")]
    [KaleidoAuthorization(Roles = "reviewer")]
    public sealed record HandOffStep : IProcessStep;

    public sealed class HandOffHandler : IProcessStepHandler<HandOffStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(HandOffStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            var target = new ProcessResult
            {
                ProcessId = context.ProcessId,
                ServiceVersion = "2",
                State = ProcessExecutionState.AwaitingStepSelection,
                AvailableSteps = ["FinishStep"],
                Steps = [new ProcessStepResult
                {
                    StepName = "BeginStep",
                    CandidateStatus = StepCandidateStatus.Built,
                    IncludedInExecutionPlan = true,
                    ExecutionStatus = StepExecutionStatus.Completed,
                }],
            };
            return Task.FromResult(ProcessStepHandlerResult.HandOff("target", target, "BeginStep"));
        }
    }

    public sealed class StoredProcesses
    {
        private int _refuseNextSave;
        public Dictionary<ProcessKey, ProcessContext> States { get; } = [];
        public object Gate { get; } = new();
        public void RefuseNextSave() => Volatile.Write(ref _refuseNextSave, 1);
        public bool TakeRefusal() => Interlocked.Exchange(ref _refuseNextSave, 0) == 1;
    }

    public sealed class TestProcessContextStore(StoredProcesses state) : IProcessContextStore
    {
        public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                return Task.FromResult(state.States.GetValueOrDefault(key));
            }
        }

        public Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (state.TakeRefusal())
                {
                    throw new ProcessConcurrencyException(context.ProcessId);
                }

                var stored = state.States.GetValueOrDefault(context.Key);
                if ((stored?.Version ?? 0) != context.Version)
                {
                    throw new ProcessConcurrencyException(context.ProcessId);
                }

                var saved = context with { Version = context.Version + 1 };
                state.States[context.Key] = saved;
                return Task.FromResult(saved);
            }
        }
    }

    public sealed class StoredIdempotency
    {
        public Dictionary<string, IdempotencyRecord> Records { get; } = new(StringComparer.Ordinal);
        public object Gate { get; } = new();
    }

    public sealed class TestIdempotencyStore(StoredIdempotency state, TimeProvider time) : IProcessIdempotencyStore
    {
        public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (state.Records.TryGetValue(admission.Key, out var existing) && existing.ExpiresUtc > time.GetUtcNow())
                {
                    return Task.FromResult<IdempotencyRecord?>(existing);
                }

                state.Records[admission.Key] = admission;
                return Task.FromResult<IdempotencyRecord?>(null);
            }
        }

        public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (!state.Records.TryGetValue(result.Key, out var existing)
                    || existing.AdmissionId != result.AdmissionId
                    || existing.State != IdempotencyState.InProgress
                    || existing.ExpiresUtc <= time.GetUtcNow())
                {
                    return Task.FromResult(false);
                }

                state.Records[result.Key] = result;
                return Task.FromResult(true);
            }
        }

        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                return Task.FromResult(state.Records.TryGetValue(key, out var existing)
                    && existing.AdmissionId == admissionId
                    && existing.State == IdempotencyState.InProgress
                    && existing.ExpiresUtc > time.GetUtcNow()
                    && state.Records.Remove(key));
            }
        }
    }
}
