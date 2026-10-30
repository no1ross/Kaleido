using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Http.Client;

namespace Kaleido.Http.FunctionalTests.Processor.Behavior;

// The process rules as a consumer meets them over HTTP: relationships between steps, the handler
// outcomes (success, rejection, unexpected failure, handoff) and what each leaves behind in the
// process state. Every scenario reads the state back, so a runtime that answers correctly but saves
// the wrong thing (or the right thing at the wrong time) fails here.
public sealed class ProcessBehaviorTests(BehaviorFixture fixture) : IClassFixture<BehaviorFixture>
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AStepWhoseDependencyIsNotDone_DoesNotRun_AndSaysWhichDependencyIsMissing()
    {
        var (status, response) = await PostStepAsync(BehaviorSteps.Close);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual("completed", Outcome(response));
        Assert.Contains("pro_dependency_not_satisfied", FrameworkCodes(response));

        var state = await StateAsync(ProcessId(response));
        Assert.NotEqual("completed", StepStatus(state, BehaviorSteps.Close));
    }

    [Fact]
    public async Task CompletingAStep_MakesItsDependentsAvailable()
    {
        var processId = await StartAsync();

        var state = await StateAsync(processId);

        Assert.Equal("completed", StepStatus(state, BehaviorSteps.Start));
        Assert.Contains(BehaviorSteps.Close, AvailableSteps(state));
        Assert.DoesNotContain(BehaviorSteps.Start, AvailableSteps(state));
        Assert.DoesNotContain(BehaviorSteps.Archive, AvailableSteps(state));
    }

    // A retry of a finished step is told it is done, not refused (#504): the consistency check answers
    // it before availability would reject it as pro_step_not_available.
    [Fact]
    public async Task ANonRepeatableStep_RunsOnlyOnce_AndASecondSendIsToldItIsAlreadyProcessed()
    {
        var processId = await StartAsync();

        var (_, again) = await PostStepAsync(BehaviorSteps.Start, processId: processId);

        Assert.NotEqual("completed", Outcome(again));
        Assert.Equal([ProcessorErrorCodes.AlreadyProcessed], FrameworkCodes(again));
        Assert.Equal("information", Assert.Single(again.GetProperty("frameworkMessages").EnumerateArray()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task ARepeatableStep_RunsAgain_AndAvailableUntilClosesItWhenItsStepCompletes()
    {
        var processId = await StartAsync();

        var (_, first) = await PostStepAsync(BehaviorSteps.Note, """{"text":"one"}""", processId);
        var (_, second) = await PostStepAsync(BehaviorSteps.Note, """{"text":"two"}""", processId);
        Assert.Equal("completed", Outcome(first));
        Assert.Equal("completed", Outcome(second));
        Assert.Contains(BehaviorSteps.Note, AvailableSteps(await StateAsync(processId)));

        var (_, close) = await PostStepAsync(BehaviorSteps.Close, processId: processId);
        Assert.Equal("completed", Outcome(close));

        var closed = await StateAsync(processId);
        Assert.DoesNotContain(BehaviorSteps.Note, AvailableSteps(closed));
        Assert.Contains(BehaviorSteps.Archive, AvailableSteps(closed));

        var (_, late) = await PostStepAsync(BehaviorSteps.Note, """{"text":"late"}""", processId);
        Assert.NotEqual("completed", Outcome(late));
        Assert.Contains("pro_step_not_available", FrameworkCodes(late));
    }

    [Fact]
    public async Task ARejectedStep_ChangesNothing_AndTellsTheCallerWhatToCorrect()
    {
        var processId = await StartAsync();
        var before = await StateAsync(processId);

        var (status, rejected) = await PostStepAsync(BehaviorSteps.Note, $$"""{"text":"{{BehaviorSteps.RejectedNote}}"}""", processId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("rejected", Outcome(rejected));
        Assert.Contains(BehaviorSteps.RejectionCode, rejected.GetProperty("businessMessages").EnumerateArray().Select(x => x.GetProperty("code").GetString()));

        var after = await StateAsync(processId);
        Assert.Equal(before.GetProperty("state").GetString(), after.GetProperty("state").GetString());
        Assert.Equal(AvailableSteps(before), AvailableSteps(after));
        Assert.NotEqual("completed", StepStatus(after, BehaviorSteps.Note));

        var (_, corrected) = await PostStepAsync(BehaviorSteps.Note, """{"text":"fine"}""", processId);
        Assert.Equal("completed", Outcome(corrected));
    }

    [Fact]
    public async Task AnInputThatBreaksItsValidationRules_IsNotRun_AndLeavesTheProcessAsItWas()
    {
        var processId = await StartAsync();
        var before = await StateAsync(processId);

        var (_, response) = await PostStepAsync(BehaviorSteps.Note, """{"text":"far too long for ten"}""", processId);

        Assert.NotEqual("completed", Outcome(response));
        Assert.NotEmpty(FrameworkCodes(response));
        var after = await StateAsync(processId);
        Assert.Equal(AvailableSteps(before), AvailableSteps(after));
        Assert.NotEqual("completed", StepStatus(after, BehaviorSteps.Note));
    }

    [Fact]
    public async Task AnUnexpectedHandlerFailure_HidesItsDetail_KeepsTheProcess_AndTheStepCanBeRetried()
    {
        var processId = await StartAsync();
        var before = await StateAsync(processId);
        fixture.Switches.Explode = true;

        try
        {
            using var raw = await SendStepAsync(BehaviorSteps.Explode, "{}", processId);
            var body = await raw.Content.ReadAsStringAsync();

            Assert.DoesNotContain(BehaviorSteps.ExplosionDetail, body, StringComparison.Ordinal);
            Assert.NotEqual("completed", Outcome(Parse(body)));

            var failed = await StateAsync(processId);
            Assert.Equal("exception", StepStatus(failed, BehaviorSteps.Explode));
            Assert.Equal(before.GetProperty("state").GetString(), failed.GetProperty("state").GetString());
            Assert.Equal(AvailableSteps(before), AvailableSteps(failed));
        }
        finally
        {
            fixture.Switches.Explode = false;
        }

        var (_, retried) = await PostStepAsync(BehaviorSteps.Explode, processId: processId);
        Assert.Equal("completed", Outcome(retried));
        Assert.Equal("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Explode));
    }

    [Fact]
    public async Task AHandOff_NamesTheTargetProcessor_InTheResponseAndTheState()
    {
        var processId = await StartAsync();

        var (_, response) = await PostStepAsync(BehaviorSteps.HandOff, processId: processId);

        Assert.Equal("completed", Outcome(response));
        Assert.Equal(BehaviorSteps.HandOffTarget, response.GetProperty("targetProcessorName").GetString());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("requiredStep").ValueKind);

        var state = await StateAsync(processId);
        Assert.Equal("handOff", state.GetProperty("state").GetString());
        Assert.Equal(BehaviorSteps.HandOffTarget, state.GetProperty("targetProcessorName").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("requiredStep").ValueKind);
    }

    // A handoff to the service itself is the handler's defect (#522): before, it silently continued
    // here; now it is a process violation that, like an exception, leaves the process as it was.
    [Fact]
    public async Task AHandOffToThisServiceItself_IsAViolation_AndLeavesTheProcessAsItWas()
    {
        var processId = await StartAsync();
        var before = await StateAsync(processId);

        var (status, response) = await PostStepAsync(BehaviorSteps.HandOffHere, processId: processId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual("completed", Outcome(response));
        Assert.Contains(ProcessorErrorCodes.InvalidHandOff, FrameworkCodes(response));
        Assert.Equal(JsonValueKind.Null, response.GetProperty("targetProcessorName").ValueKind);

        var after = await StateAsync(processId);
        Assert.Equal("exception", StepStatus(after, BehaviorSteps.HandOffHere));
        Assert.Equal(before.GetProperty("state").GetString(), after.GetProperty("state").GetString());
        Assert.Equal(AvailableSteps(before), AvailableSteps(after));
        Assert.Equal(JsonValueKind.Null, after.GetProperty("targetProcessorName").ValueKind);
    }

    // Only the caller's cancellation is a cancellation (#517): a handler whose downstream call times out
    // failed - the step is an exception and retryable, the process is unchanged, the answer is not a
    // silent 200 with an empty body.
    [Fact]
    public async Task AHandlerTimeout_IsAnException_NotACancellation_AndLeavesTheProcessAsItWas()
    {
        var processId = await StartAsync();
        var before = await StateAsync(processId);

        var (status, body) = await PostStepAsync(BehaviorSteps.TimeOut, processId: processId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("exception", Outcome(body));
        var after = await StateAsync(processId);
        Assert.Equal("exception", StepStatus(after, BehaviorSteps.TimeOut));
        Assert.Equal(before.GetProperty("state").GetString(), after.GetProperty("state").GetString());
        Assert.Equal(AvailableSteps(before), AvailableSteps(after));
    }

    // The lifecycle (#512): an ended process admits no further step, and its state says so - what it
    // shows and what it admits agree.
    [Fact]
    public async Task AFailedStep_EndsTheProcess_AndNoFurtherStepRuns()
    {
        var processId = await StartAsync();
        Assert.Contains(BehaviorSteps.Note, AvailableSteps(await StateAsync(processId)));

        var (_, failed) = await PostStepAsync(BehaviorSteps.Fail, processId: processId);
        Assert.Equal("failed", Outcome(failed));

        var ended = await StateAsync(processId);
        Assert.Equal("businessFailure", ended.GetProperty("state").GetString());
        Assert.Empty(AvailableSteps(ended));

        var (_, late) = await PostStepAsync(BehaviorSteps.Note, """{"text":"late"}""", processId);
        Assert.Equal("pending", Outcome(late));
        Assert.Contains(ProcessorErrorCodes.ProcessEnded, FrameworkCodes(late));
        Assert.NotEqual("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Note));
    }

    // Complete() ends the process whatever its rules still offer (#534): Note was available.
    [Fact]
    public async Task AStepThatCompletesTheProcess_EndsIt_ThoughOtherStepsWereAvailable()
    {
        var processId = await StartAsync();
        Assert.Contains(BehaviorSteps.Note, AvailableSteps(await StateAsync(processId)));

        var (_, finished) = await PostStepAsync(BehaviorSteps.Finish, processId: processId);
        Assert.Equal("completed", Outcome(finished));
        Assert.Empty(finished.GetProperty("availableSteps").EnumerateArray());

        var complete = await StateAsync(processId);
        Assert.Equal("complete", complete.GetProperty("state").GetString());
        Assert.Empty(AvailableSteps(complete));

        var (_, late) = await PostStepAsync(BehaviorSteps.Note, """{"text":"late"}""", processId);
        Assert.Equal("pending", Outcome(late));
        Assert.Contains(ProcessorErrorCodes.ProcessEnded, FrameworkCodes(late));
    }

    // After a handoff the target owns the process: this service runs none of its steps, and the target
    // name is kept (#512). A local step used to run and erase it.
    [Fact]
    public async Task AfterAHandOff_ThisServiceRunsNoStep_AndKeepsTheTarget()
    {
        var processId = await StartAsync();
        await PostStepAsync(BehaviorSteps.HandOff, processId: processId);

        foreach (var caller in new string?[] { null, "billing" })
        {
            var (_, late) = await PostStepAsync(BehaviorSteps.Note, """{"text":"late"}""", processId, callingProcessor: caller);
            Assert.Equal("pending", Outcome(late));
            Assert.Contains(ProcessorErrorCodes.ProcessEnded, FrameworkCodes(late));
        }

        var state = await StateAsync(processId);
        Assert.Equal("handOff", state.GetProperty("state").GetString());
        Assert.Equal(BehaviorSteps.HandOffTarget, state.GetProperty("targetProcessorName").GetString());
        Assert.Empty(AvailableSteps(state));
    }

    // The service the process was handed to may hand it back: it is this service's again (#512).
    [Fact]
    public async Task AHandBackFromTheTarget_ReopensTheProcessHere()
    {
        var processId = await StartAsync();
        await PostStepAsync(BehaviorSteps.HandOff, processId: processId);

        var (_, back) = await PostStepAsync(BehaviorSteps.Note, """{"text":"back"}""", processId, callingProcessor: "RADIOLOGY");

        Assert.Equal("completed", Outcome(back));
        var state = await StateAsync(processId);
        Assert.NotEqual("handOff", state.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("targetProcessorName").ValueKind);
        Assert.Contains(BehaviorSteps.Note, AvailableSteps(state));
    }

    [Fact]
    public async Task AProcessHandedOffByAnotherProcessor_StartsHereUnderItsId()
    {
        var processId = Guid.NewGuid();

        var (status, response) = await PostStepAsync(BehaviorSteps.Start, processId: processId, callingProcessor: "intake");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("completed", Outcome(response));
        Assert.Equal(processId, ProcessId(response));
        Assert.Equal("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Start));
    }

    [Fact]
    public async Task AnUnknownProcessId_FromThisServiceItself_IsNotAHandOff()
    {
        var (status, _) = await PostStepAsync(BehaviorSteps.Start, processId: Guid.NewGuid(), callingProcessor: BehaviorFixture.ServiceName);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task TwoRequestsForTheSameProcessAtOnce_TheSecondSaveIsAConflict_AndSavesNothing()
    {
        var processId = await StartAsync();
        fixture.Gate.Reset();

        var slow = PostStepAsync(BehaviorSteps.Slow, processId: processId);
        await fixture.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var (quickStatus, quick) = await PostStepAsync(BehaviorSteps.Quick, processId: processId);
        Assert.Equal(HttpStatusCode.OK, quickStatus);
        Assert.Equal("completed", Outcome(quick));

        fixture.Gate.Release.TrySetResult();
        var (slowStatus, conflict) = await slow;

        Assert.Equal(HttpStatusCode.Conflict, slowStatus);
        Assert.Equal("pro_process_conflict", conflict.GetProperty("errors")[0].GetProperty("code").GetString());

        var state = await StateAsync(processId);
        Assert.Equal("completed", StepStatus(state, BehaviorSteps.Quick));
        Assert.NotEqual("completed", StepStatus(state, BehaviorSteps.Slow));
    }

    [Fact]
    public async Task SeveralStepsInOneExecute_RunInDependencyOrder_WhateverTheOrderSubmitted()
    {
        using var response = await PostExecuteAsync(
            $$$"""{"steps":[{"stepName":"{{{BehaviorSteps.Close}}}","request":{}},{"stepName":"{{{BehaviorSteps.Start.ToLowerInvariant()}}}","request":{}}]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = Parse(await response.Content.ReadAsStringAsync());

        var state = await StateAsync(ProcessId(body));
        Assert.Equal("completed", StepStatus(state, BehaviorSteps.Start));
        Assert.Equal("completed", StepStatus(state, BehaviorSteps.Close));
        Assert.Contains(BehaviorSteps.Archive, AvailableSteps(state));
    }

    [Fact]
    public async Task AnExecute_WithAProcessId_ResumesThatProcess()
    {
        var processId = await StartAsync();

        using var response = await PostExecuteAsync($$$"""{"steps":[{"stepName":"{{{BehaviorSteps.Close}}}","request":{}}]}""", processId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(processId, ProcessId(Parse(await response.Content.ReadAsStringAsync())));
        Assert.Equal("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Close));
    }

    [Fact]
    public async Task AnExecuteRetriedWithTheSameIdempotencyKey_IsAnsweredOnce_AndRunsOnce()
    {
        var key = Guid.NewGuid().ToString();
        var body = $$$"""{"steps":[{"stepName":"{{{BehaviorSteps.Start}}}","request":{}}]}""";

        using var first = await PostExecuteAsync(body, idempotencyKey: key);
        using var second = await PostExecuteAsync(body, idempotencyKey: key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.TryGetValues("Idempotent-Replayed", out var replayed));
        Assert.Equal("true", Assert.Single(replayed));
        Assert.Equal(
            ProcessId(Parse(await first.Content.ReadAsStringAsync())),
            ProcessId(Parse(await second.Content.ReadAsStringAsync())));
    }

    // #285: a retry with the same key while the first attempt still runs does not run the step; the
    // caller is told to come back, and then gets the first attempt's answer. The handler ran once.
    [Fact]
    public async Task ARetryWhileTheFirstAttemptRuns_DoesNotRunTheStep_AndThenGetsItsAnswer()
    {
        var processId = await StartAsync();
        fixture.Gate.Reset();
        var key = Guid.NewGuid().ToString();
        var body = SlowBody();

        var first = PostExecuteAsync(body, processId, key);
        await fixture.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        using (var duplicate = await PostExecuteAsync(body, processId, key))
        {
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal("pro_idempotency_in_progress", ErrorCode(Parse(await duplicate.Content.ReadAsStringAsync())));
            Assert.NotNull(duplicate.Headers.RetryAfter);
        }

        fixture.Gate.Release.TrySetResult();
        using (var answered = await first)
        {
            Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        }

        using var replayed = await PostExecuteAsync(body, processId, key);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.True(replayed.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, fixture.Gate.Invocations);
        Assert.Equal("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Slow));
    }

    // #285: the caller gave up (a client timeout) while the step ran; the step still finished, and
    // its answer was stored, so the retry gets it instead of "already completed" - and nothing runs twice.
    [Fact]
    public async Task WhenTheCallerGivesUpWhileTheStepRuns_TheRetryGetsTheStepsAnswer()
    {
        var processId = await StartAsync();
        fixture.Gate.Reset();
        var key = Guid.NewGuid().ToString();
        var body = SlowBody();

        using var abandon = new CancellationTokenSource();
        var first = PostExecuteAsync(body, processId, key, abandon.Token);
        await fixture.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await abandon.CancelAsync();

        // The step finishes after its caller has gone (TestServer completes the abandoned call only then).
        fixture.Gate.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var retry = await RetryUntilAnsweredAsync(body, processId, key);

        Assert.Equal(HttpStatusCode.OK, retry.Status);
        Assert.True(retry.Replayed);
        Assert.Equal(1, fixture.Gate.Invocations);
        Assert.Equal("completed", StepStatus(await StateAsync(processId), BehaviorSteps.Slow));
    }

    private static string SlowBody() =>
        $$$"""{"steps":[{"stepName":"{{{BehaviorSteps.Slow}}}","request":{}}]}""";

    private static string? ErrorCode(JsonElement body) =>
        body.GetProperty("errors")[0].GetProperty("code").GetString();

    // Retries like a client honouring Retry-After, until the key is no longer in progress.
    private async Task<(HttpStatusCode Status, bool Replayed)> RetryUntilAnsweredAsync(string body, Guid processId, string key)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await PostExecuteAsync(body, processId, key);

            if (response.StatusCode != HttpStatusCode.Conflict || attempt == 50)
            {
                return (response.StatusCode, response.Headers.Contains("Idempotent-Replayed"));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AnErrorResponse_StillEchoesTheCallersRequestId()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, StepUrl(BehaviorSteps.Note))
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(KaleidoCorrelationHeaders.RequestId, requestId);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var echoed));
        Assert.Equal(requestId, Assert.Single(echoed));
    }

    [Fact]
    public async Task AnUnexpectedServerFailure_IsABare500_WithTheErrorShape_AndTheRequestId()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{BehaviorFixture.ServiceName}/queryable/behaviorbrokensource/query")
        {
            Content = new StringContent("""{"query":{}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(KaleidoCorrelationHeaders.RequestId, requestId);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(BehaviorSteps.ExplosionDetail, body, StringComparison.Ordinal);
        Assert.Equal("framework_error", Parse(body).GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var echoed));
        Assert.Equal(requestId, Assert.Single(echoed));
    }

    [Fact]
    public async Task AMalformedProcessIdHeader_IsA400ArgumentError_WithTheErrorShape()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, StepUrl(BehaviorSteps.Start))
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, "not-a-guid");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("argument_error", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheTypedClient_RunsAndResumesAProcess_AndReadsItsState()
    {
        var started = await fixture.ProcessClient.ExecuteStepAsync(new BehaviorStartStep(), TestContext.Current.CancellationToken);
        Assert.Equal(StepExecutionStatus.Completed, started.Status);
        Assert.NotEqual(Guid.Empty, started.ProcessId);

        var closed = await fixture.ProcessClient.ExecuteStepAsync(
            BehaviorSteps.Close,
            JsonSerializer.SerializeToElement(new { }),
            started.ProcessId,
            idempotencyKey: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(StepExecutionStatus.Completed, closed.Status);
        Assert.Equal(started.ProcessId, closed.ProcessId);

        var state = await fixture.ProcessClient.GetProcessStateAsync(started.ProcessId, TestContext.Current.CancellationToken);
        Assert.NotNull(state);
        Assert.Contains(state.Steps, x => x.StepName == BehaviorSteps.Close && x.Status == StepExecutionStatus.Completed);
        Assert.Contains(state.AvailableSteps, x => x.Name == BehaviorSteps.Archive);
    }

    // #440: a caller repeating a logical call with its own key gets the stored answer, through the real
    // client and server. Quick is repeatable, so every run is recorded: the keyed repeat leaves the
    // step's last execution unchanged (it did not run), and a call under another key runs it again.
    [Fact]
    public async Task TheTypedClient_RepeatingACallWithTheSameIdempotencyKey_GetsTheStoredAnswer_WithoutRunningTheStep()
    {
        var started = await fixture.ProcessClient.ExecuteStepAsync(new BehaviorStartStep(), TestContext.Current.CancellationToken);
        var key = $"quick-{started.ProcessId:N}";

        Task<Kaleido.Http.Processor.StepExecutionResponse<JsonElement>> QuickAsync(string idempotencyKey) =>
            fixture.ProcessClient.ExecuteStepAsync(
                BehaviorSteps.Quick,
                JsonSerializer.SerializeToElement(new { }),
                started.ProcessId,
                idempotencyKey,
                TestContext.Current.CancellationToken);

        async Task<DateTimeOffset?> LastRunAsync() =>
            (await fixture.ProcessClient.GetProcessStateAsync(started.ProcessId, TestContext.Current.CancellationToken))!
                .Steps.Single(x => x.StepName == BehaviorSteps.Quick).LastExecuted;

        var first = await QuickAsync(key);
        var ranAt = await LastRunAsync();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var repeated = await QuickAsync(key);

        Assert.Equal(StepExecutionStatus.Completed, first.Status);
        Assert.Equal(first.Status, repeated.Status);
        Assert.Equal(ranAt, await LastRunAsync());

        await QuickAsync($"other-{started.ProcessId:N}");
        Assert.NotEqual(ranAt, await LastRunAsync());
    }

    // #503: the answer to an execute is lost after the step ran (a dropped connection). The client's
    // resilience pipeline retries the POST because it carries an Idempotency-Key - the same key, so the
    // service replays its stored answer and the handler runs once. Catches a retry that mints a new key
    // (the step would run twice) or a keyed POST that is not retried at all.
    [Fact]
    public async Task AnExecuteWhoseAnswerIsLost_IsRetriedWithTheSameKey_AndTheStepRunsOnce()
    {
        var cancel = TestContext.Current.CancellationToken;
        var started = await fixture.ProcessClient.ExecuteStepAsync(new BehaviorStartStep(), cancel);
        fixture.Gate.Reset();
        fixture.Gate.Release.TrySetResult();

        var losing = new LoseTheFirstAnswer();
        var (client, provider) = fixture.CreateProcessClient(
            server =>
            {
                losing.InnerHandler = server;
                return losing;
            },
            new Dictionary<string, string?> { ["Kaleido:Resilience:RetryDelay"] = "00:00:00.010" });

        using (provider)
        {
            var slow = await client.ExecuteStepAsync(BehaviorSteps.Slow, JsonSerializer.SerializeToElement(new { }), started.ProcessId, idempotencyKey: null, cancel);

            Assert.Equal(StepExecutionStatus.Completed, slow.Status);
        }

        Assert.Equal(1, fixture.Gate.Invocations);
        Assert.Equal(2, losing.Keys.Count);
        Assert.Single(losing.Keys.Distinct(StringComparer.Ordinal));
    }

    // Lets the first execute reach the service, then drops its answer as a lost connection would.
    private sealed class LoseTheFirstAnswer : DelegatingHandler
    {
        public List<string?> Keys { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.TryGetValues(KaleidoIdempotencyHeaders.IdempotencyKey, out var values) ? values.Single() : null);

            var response = await base.SendAsync(request, cancellationToken);

            if (Keys.Count > 1)
            {
                return response;
            }

            response.Dispose();
            throw new HttpRequestException("The connection was lost after the request was sent.");
        }
    }

    [Fact]
    public async Task TheTypedClient_ReportsAnUnknownProcessId_AsAValidationFailure()
    {
        var exception =
            await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
                fixture.ProcessClient.ExecuteStepAsync(
                    BehaviorSteps.Close,
                    JsonSerializer.SerializeToElement(new { }),
                    Guid.NewGuid(),
                    idempotencyKey: null,
                    TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains(exception.Errors, x => x.Code == "pro_process_not_found");
    }

    private static string StepUrl(string step) => $"/{BehaviorFixture.ServiceName}/processes/steps/{step.ToLowerInvariant()}";

    private async Task<Guid> StartAsync()
    {
        var (status, response) = await PostStepAsync(BehaviorSteps.Start);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("completed", Outcome(response));
        return ProcessId(response);
    }

    private async Task<HttpResponseMessage> SendStepAsync(string step, string processStepJson, Guid? processId, string? callingProcessor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StepUrl(step))
        {
            Content = new StringContent($$"""{"processStep":{{processStepJson}}}""", Encoding.UTF8, "application/json"),
        };

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        if (callingProcessor is not null)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, callingProcessor);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostStepAsync(string step, string processStepJson = "{}", Guid? processId = null, string? callingProcessor = null)
    {
        using var response = await SendStepAsync(step, processStepJson, processId, callingProcessor);
        return (response.StatusCode, Parse(await response.Content.ReadAsStringAsync()));
    }

    private async Task<HttpResponseMessage> PostExecuteAsync(string json, Guid? processId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/{BehaviorFixture.ServiceName}/processes/execute")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, TestContext.Current.CancellationToken);
        return await _client.SendAsync(request, linked.Token);
    }

    private async Task<JsonElement> StateAsync(Guid processId)
    {
        using var response = await _client.GetAsync($"/{BehaviorFixture.ServiceName}/processes/{processId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"State read failed ({(int)response.StatusCode}): {body}");
        return Parse(body);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Guid ProcessId(JsonElement body) => body.GetProperty("processId").GetGuid();

    private static string? Outcome(JsonElement body) => body.GetProperty("status").GetString();

    private static string?[] FrameworkCodes(JsonElement body) =>
        body.GetProperty("frameworkMessages").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();

    private static string?[] AvailableSteps(JsonElement state) =>
        state.GetProperty("availableSteps").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order(StringComparer.Ordinal).ToArray();

    private static string? StepStatus(JsonElement state, string step) =>
        state.GetProperty("steps").EnumerateArray()
            .Where(x => x.GetProperty("stepName").GetString() == step)
            .Select(x => x.GetProperty("status").GetString())
            .SingleOrDefault();
}
