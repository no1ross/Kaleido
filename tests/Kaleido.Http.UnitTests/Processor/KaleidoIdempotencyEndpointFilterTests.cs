using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class KaleidoIdempotencyEndpointFilterTests
    : SutFixture
{
    private readonly Mock<IKaleidoIdempotencyStore> store = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly Mock<ILogger<KaleidoIdempotencyEndpointFilter>> logger = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly List<IdempotentResult> admitted = [];
    private readonly List<IdempotentResult> saved = [];
    private readonly List<(string Key, Guid AdmissionId)> released = [];
    private int calls;

    public KaleidoIdempotencyEndpointFilterTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerName = "alice" });
        store.Setup(x => x.AdmitAsync(It.IsAny<IdempotentResult>(), It.IsAny<CancellationToken>()))
            .Callback<IdempotentResult, CancellationToken>((admission, _) => admitted.Add(admission))
            .ReturnsAsync((IdempotentResult?)null);
        store.Setup(x => x.CompleteAsync(It.IsAny<IdempotentResult>(), CancellationToken.None))
            .Callback<IdempotentResult, CancellationToken>((result, _) => saved.Add(result))
            .ReturnsAsync(true);
        store.Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), CancellationToken.None))
            .Callback<string, Guid, CancellationToken>((key, admissionId, _) => released.Add((key, admissionId)))
            .ReturnsAsync(true);
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    // The store already holds the key with this record.
    private void Holding(IdempotentResult existing) =>
        store.Setup(x => x.AdmitAsync(It.Is<IdempotentResult>(r => r.Key == existing.Key), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

    private KaleidoIdempotencyEndpointFilter CreateSut() =>
        new(
            store.Object,
            new KaleidoHttpOptions { IdempotencyRetention = TimeSpan.FromHours(2), IdempotencyUnknownOutcomeRetention = TimeSpan.FromDays(3) },
            new KaleidoServiceOptions { ServiceName = "svc" },
            correlation.Object,
            time,
            logger.Object);

    private ValueTask<object?> Endpoint(EndpointFilterInvocationContext _)
    {
        calls++;
        return ValueTask.FromResult<object?>(Results.Ok(new { processId = "p-1" }));
    }

    private static DefaultEndpointFilterInvocationContext Context(string? key, object? body = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/svc/processes/steps/start";

        if (key is not null)
        {
            http.Request.Headers[KaleidoIdempotencyHeaders.IdempotencyKey] = key;
        }

        return new DefaultEndpointFilterInvocationContext(http, body ?? new { step = "start" });
    }

    [Fact]
    public async Task WithoutAKey_RunsTheEndpointAndStoresNothing()
    {
        await CreateSut().InvokeAsync(Context(key: null), Endpoint);

        Assert.Equal(1, calls);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WithANewKey_RunsTheEndpointAndStoresItsAnswer_ScopedPerServiceAndCaller()
    {
        await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(1, calls);
        var admission = Assert.Single(admitted);
        Assert.Equal(IdempotentResultState.InProgress, admission.State);
        Assert.Equal(time.GetUtcNow().AddMinutes(5), admission.LeaseUntilUtc);
        var stored = Assert.Single(saved);
        Assert.Equal("svc:name:alice:k1", stored.Key);
        Assert.Equal(IdempotentResultState.Completed, stored.State);
        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Equal("""{"processId":"p-1"}""", stored.Response);
        Assert.Equal(time.GetUtcNow().AddHours(2), stored.ExpiresUtc);
    }

    [Theory]
    [InlineData("alice", "sub-42", "svc:id:sub-42:k1")]
    [InlineData(null, "sub-42", "svc:id:sub-42:k1")]
    [InlineData(null, null, "svc:anonymous:k1")]
    public async Task TheKeyIsScopedToTheCallersSubjectId_ElseItsName_ElseAnonymous(string? name, string? id, string expected)
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerName = name, CallerId = id });

        await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(expected, Assert.Single(saved).Key);
    }

    [Fact]
    public async Task WithAStoredKeyAndTheSameRequest_ReplaysTheAnswerWithoutRunningTheEndpoint()
    {
        await CreateSut().InvokeAsync(Context("k1"), Endpoint);
        Holding(saved[0]);
        var retry = Context("k1");

        var result = await CreateSut().InvokeAsync(retry, Endpoint);

        Assert.Equal(1, calls);
        var replay = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("""{"processId":"p-1"}""", replay.ResponseContent);
        Assert.Equal("true", retry.HttpContext.Response.Headers[KaleidoIdempotencyHeaders.Replayed].ToString());
    }

    [Fact]
    public async Task WithAStoredKeyAndADifferentRequest_Answers422WithoutRunningTheEndpoint()
    {
        await CreateSut().InvokeAsync(Context("k1"), Endpoint);
        Holding(saved[0]);

        var result = await CreateSut().InvokeAsync(Context("k1", new { step = "other" }), Endpoint);

        Assert.Equal(1, calls);
        var error = Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.StatusCode);
        Assert.Equal("pro_idempotency_key_reused", Assert.Single(error.Value!.Errors).Code);
    }

    [Fact]
    public async Task WithAKeyThatIsTooLong_Answers400WithoutRunningTheEndpoint()
    {
        var result = await CreateSut().InvokeAsync(Context(new string('k', 201)), Endpoint);

        Assert.Equal(0, calls);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result).StatusCode);
    }

    [Fact]
    public async Task ASuccessfulAnswerWithoutAValue_IsStoredWithAnEmptyBody()
    {
        var result = await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.NoContent()));

        Assert.IsType<NoContent>(result);
        var stored = Assert.Single(saved);
        Assert.Equal(IdempotentResultState.Completed, stored.State);
        Assert.Equal(StatusCodes.Status204NoContent, stored.StatusCode);
        Assert.Empty(stored.Response);
        Assert.Empty(released);
    }

    [Fact]
    public async Task AValueAnswerWithoutAStatusCode_IsStoredAs200()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(new ValueOnlyResult(new { processId = "p-2" })));

        var stored = Assert.Single(saved);
        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Equal("""{"processId":"p-2"}""", stored.Response);
    }

    [Fact]
    public async Task ASuccessfulAnswer_IsStoredWithItsStatusCode()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Created("/p-3", new { processId = "p-3" })));

        Assert.Equal(StatusCodes.Status201Created, Assert.Single(saved).StatusCode);
    }

    [Fact]
    public async Task AnAnswerThatIsNeither2xxNor4xx_MarksTheOutcomeUnknown()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Json(new { }, JsonSerializerOptions.Default, statusCode: StatusCodes.Status102Processing)));

        Assert.Equal(IdempotentResultState.OutcomeUnknown, Assert.Single(saved).State);
    }

    [Fact]
    public async Task ARequestWithoutArguments_IsHashedByRouteAndProcessOnly()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/svc/processes/execute";
        http.Request.Headers[KaleidoIdempotencyHeaders.IdempotencyKey] = "k1";
        http.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = "p-9";

        await CreateSut().InvokeAsync(new DefaultEndpointFilterInvocationContext(http), Endpoint);

        Assert.Equal(Sha256("POST /svc/processes/execute\np-9\n"), Assert.Single(saved).RequestHash);
    }

    [Fact]
    public async Task AJsonBodyArgument_IsHashedAsSent()
    {
        var body = Mock.Of<IKaleidoJsonBody>(x => x.RawJson == """{ "step" : "start" }""");

        await CreateSut().InvokeAsync(Context("k1", body), Endpoint);

        Assert.Equal(
            Sha256("POST /svc/processes/steps/start\n\n{ \"step\" : \"start\" }"),
            Assert.Single(saved).RequestHash);
    }

    private static string Sha256(string payload) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private sealed class ValueOnlyResult(object value)
        : IResult, IValueHttpResult
    {
        public object? Value => value;

        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }

    [Fact]
    public async Task AFailedAnswer_IsNotStored()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Json(new { error = "no" }, JsonSerializerOptions.Default, statusCode: StatusCodes.Status400BadRequest)));

        Assert.Empty(saved);
        Assert.Equal("svc:name:alice:k1", Assert.Single(released).Key);
    }

    // #285: a retry while the first attempt still runs does not run; it is told to come back.
    [Fact]
    public async Task WithAKeyStillInProgress_Answers409WithRetryAfter_WithoutRunningTheEndpoint()
    {
        Holding(InProgress("svc:name:alice:k1", time.GetUtcNow().AddMinutes(1)));
        var retry = Context("k1");

        var result = await CreateSut().InvokeAsync(retry, Endpoint);

        Assert.Equal(0, calls);
        var error = Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyInProgress, Assert.Single(error.Value!.Errors).Code);
        Assert.Equal("1", retry.HttpContext.Response.Headers.RetryAfter.ToString());
        Assert.Empty(released);
    }

    // #285: an attempt that never finished may have run: the key is never run again.
    [Fact]
    public async Task WithAKeyWhoseLeaseExpired_AnswersOutcomeUnknown_WithoutRunningTheEndpoint()
    {
        Holding(InProgress("svc:name:alice:k1", time.GetUtcNow().AddMinutes(-1)));
        var retry = Context("k1");

        var result = await CreateSut().InvokeAsync(retry, Endpoint);

        Assert.Equal(0, calls);
        var error = Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, Assert.Single(error.Value!.Errors).Code);
        Assert.False(retry.HttpContext.Response.Headers.ContainsKey("Retry-After"));
    }

    // The first attempt holds the key until the end of its lease: one tick before, a retry is told to
    // come back; at the end, the attempt counts as never finished.
    [Theory]
    [InlineData(1, ProcessorErrorCodes.IdempotencyInProgress)]
    [InlineData(0, ProcessorErrorCodes.IdempotencyOutcomeUnknown)]
    public async Task WithAKeyAroundTheEndOfItsLease(int ticksLeft, string code)
    {
        Holding(InProgress("svc:name:alice:k1", time.GetUtcNow() + TimeSpan.FromTicks(ticksLeft)));

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(0, calls);
        Assert.Equal(code, Assert.Single(Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result).Value!.Errors).Code);
    }

    [Fact]
    public async Task WithAKeyInProgressForADifferentRequest_Answers422()
    {
        Holding(InProgress("svc:name:alice:k1", time.GetUtcNow().AddMinutes(1)) with { RequestHash = "other" });

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(0, calls);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result).StatusCode);
    }

    [Fact]
    public async Task AnEndpointThatThrowsARefusal_ReleasesTheKey_AndTheExceptionPropagates()
    {
        await Assert.ThrowsAsync<KaleidoValidationException>(() => CreateSut().InvokeAsync(
            Context("k1"),
            _ => throw new KaleidoValidationException("code", "bad")).AsTask());

        Assert.Empty(saved);
        Assert.Equal("svc:name:alice:k1", Assert.Single(released).Key);
    }

    // #285: the caller gave up (a client timeout aborts the request) after the step ran: the answer is
    // still stored, so the client's retry gets it instead of a rejection.
    [Fact]
    public async Task WhenTheCallerHasGone_TheAnswerIsStillStored()
    {
        using var aborted = new CancellationTokenSource();
        var context = Context("k1");
        context.HttpContext.RequestAborted = aborted.Token;

        await CreateSut().InvokeAsync(context, invocation =>
        {
            aborted.Cancel();
            return Endpoint(invocation);
        });

        Assert.Single(saved);
    }

    // #441: the admission is kept for the unknown-outcome retention (not the replay retention), so an
    // attempt that may still run is never freed by the replay window; the answer for the replay window.
    [Fact]
    public async Task TheAdmissionAndTheAnswer_ExpireAfterTheirOwnRetentions()
    {
        var now = time.GetUtcNow();

        await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        var admission = Assert.Single(admitted);
        Assert.Equal(now.AddMinutes(5), admission.LeaseUntilUtc);
        Assert.Equal(now.AddDays(3), admission.ExpiresUtc);
        Assert.Equal(now.AddHours(2), Assert.Single(saved).ExpiresUtc);
    }

    // #441: every finalization carries the admission's own id, so the store can refuse it once the
    // admission no longer holds the key.
    [Fact]
    public async Task TheFinalization_CarriesTheAdmissionsId()
    {
        await CreateSut().InvokeAsync(Context("k1"), Endpoint);
        await CreateSut().InvokeAsync(Context("k2"), _ => ValueTask.FromResult<object?>(Results.BadRequest()));

        Assert.NotEqual(admitted[0].AdmissionId, admitted[1].AdmissionId);
        Assert.Equal(admitted[0].AdmissionId, Assert.Single(saved).AdmissionId);
        Assert.Equal(admitted[1].AdmissionId, Assert.Single(released).AdmissionId);
    }

    // #441: a 5xx answer, an unexpected error or a cancellation may come after the step ran: the key is
    // kept, marked unknown, for the unknown-outcome retention - never released for a second run.
    [Fact]
    public async Task AServerErrorAnswer_MarksTheOutcomeUnknown()
    {
        var result = await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Json(new { error = "down" }, JsonSerializerOptions.Default, statusCode: StatusCodes.Status503ServiceUnavailable)));

        var unknown = Assert.Single(saved);
        Assert.Equal(IdempotentResultState.OutcomeUnknown, unknown.State);
        Assert.Empty(unknown.Response);
        Assert.Equal(time.GetUtcNow().AddDays(3), unknown.ExpiresUtc);
        Assert.Empty(released);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData("unexpected")]
    [InlineData("canceled")]
    [InlineData("configuration")]
    public async Task AnEndpointThatThrowsAfterItMayHaveRun_MarksTheOutcomeUnknown_AndTheExceptionPropagates(string failure)
    {
        Exception thrown = failure switch
        {
            "unexpected" => new TimeoutException("db"),
            "canceled" => new OperationCanceledException(),
            _ => new KaleidoConfigurationException("code", "bad"),
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => CreateSut().InvokeAsync(Context("k1"), _ => throw thrown).AsTask());

        Assert.Same(thrown, exception);
        Assert.Equal(IdempotentResultState.OutcomeUnknown, Assert.Single(saved).State);
        Assert.Empty(released);
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("conflict")]
    [InlineData("argument")]
    public async Task AnEndpointThatThrowsBeforeAnythingRan_ReleasesTheKey(string refusal)
    {
        Exception thrown = refusal switch
        {
            "authorization" => new KaleidoAuthorizationException("start", callerIsAuthenticated: true),
            "conflict" => new KaleidoConcurrencyException(Guid.NewGuid()),
            _ => new ArgumentException("bad"),
        };

        await Assert.ThrowsAnyAsync<Exception>(() => CreateSut().InvokeAsync(Context("k1"), _ => throw thrown).AsTask());

        Assert.Empty(saved);
        Assert.Single(released);
    }

    [Fact]
    public async Task WithAKeyWhoseOutcomeIsUnknown_AnswersOutcomeUnknown_WithoutRunningTheEndpoint()
    {
        Holding(InProgress("svc:name:alice:k1", time.GetUtcNow().AddMinutes(1)) with { State = IdempotentResultState.OutcomeUnknown });

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(0, calls);
        var error = Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, Assert.Single(error.Value!.Errors).Code);
    }

    // #441: the request's own answer never depends on its bookkeeping: a store failure is logged at Error.
    [Fact]
    public async Task WhenTheStoreFailsToFinalize_TheAnswerIsUnchanged_AndTheFailureIsLoggedAsError()
    {
        store.Setup(x => x.CompleteAsync(It.IsAny<IdempotentResult>(), CancellationToken.None)).ThrowsAsync(new TimeoutException("store down"));

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(1, calls);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(LogLevel.Error, Assert.Single(Logs()).Level);
    }

    [Fact]
    public async Task WhenTheStoreFailsToFinalizeAFailedRequest_TheRequestsOwnExceptionPropagates()
    {
        store.Setup(x => x.CompleteAsync(It.IsAny<IdempotentResult>(), CancellationToken.None)).ThrowsAsync(new TimeoutException("store down"));
        var thrown = new InvalidCastException("handler");

        var exception = await Assert.ThrowsAsync<InvalidCastException>(() => CreateSut().InvokeAsync(Context("k1"), _ => throw thrown).AsTask());

        Assert.Same(thrown, exception);
        Assert.Equal(LogLevel.Error, Assert.Single(Logs()).Level);
    }

    // #441: the admission lost its key (it expired and the key was admitted again): nothing is written
    // over the newer record, and the request's answer is unchanged.
    [Fact]
    public async Task WhenTheStoreNoLongerAcceptsTheFinalization_TheAnswerIsUnchanged_AndAWarningIsLogged()
    {
        store.Setup(x => x.CompleteAsync(It.IsAny<IdempotentResult>(), CancellationToken.None)).ReturnsAsync(false);

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(1, calls);
        Assert.NotNull(result);
        var log = Assert.Single(Logs());
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("no longer holds the key", log.Message, StringComparison.Ordinal);
    }

    private IEnumerable<(LogLevel Level, string Message)> Logs() =>
        logger.Invocations
            .Where(x => x.Method.Name == nameof(ILogger.Log))
            .Select(x => ((LogLevel)x.Arguments[0], x.Arguments[2].ToString() ?? string.Empty));

    private IdempotentResult InProgress(string key, DateTimeOffset leaseUntilUtc) =>
        new()
        {
            Key = key,
            AdmissionId = Guid.NewGuid(),
            RequestHash = Sha256("POST /svc/processes/steps/start\n\n" + JsonSerializer.Serialize(new { step = "start" }, KaleidoJsonOptions.Options)),
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = leaseUntilUtc,
            ExpiresUtc = time.GetUtcNow().AddHours(1),
        };
}
