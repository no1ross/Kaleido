using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class KaleidoIdempotencyEndpointFilterTests
    : SutFixture
{
    private readonly Mock<IKaleidoContextStore> store = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly List<IdempotentResult> admitted = [];
    private readonly List<IdempotentResult> saved = [];
    private readonly List<string> released = [];
    private int calls;

    public KaleidoIdempotencyEndpointFilterTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerName = "alice" });
        store.Setup(x => x.AdmitAsync(It.IsAny<IdempotentResult>(), It.IsAny<CancellationToken>()))
            .Callback<IdempotentResult, CancellationToken>((admission, _) => admitted.Add(admission))
            .ReturnsAsync((IdempotentResult?)null);
        store.Setup(x => x.CompleteAsync(It.IsAny<IdempotentResult>(), CancellationToken.None))
            .Callback<IdempotentResult, CancellationToken>((result, _) => saved.Add(result))
            .Returns(Task.CompletedTask);
        store.Setup(x => x.ReleaseAsync(It.IsAny<string>(), CancellationToken.None))
            .Callback<string, CancellationToken>((key, _) => released.Add(key))
            .Returns(Task.CompletedTask);
    }

    // The store already holds the key with this record.
    private void Holding(IdempotentResult existing) =>
        store.Setup(x => x.AdmitAsync(It.Is<IdempotentResult>(r => r.Key == existing.Key), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

    private KaleidoIdempotencyEndpointFilter CreateSut() =>
        new(
            store.Object,
            new KaleidoHttpOptions { IdempotencyRetention = TimeSpan.FromHours(2) },
            new KaleidoServiceOptions { ServiceName = "svc" },
            correlation.Object);

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
        Assert.InRange(admission.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(4.9), DateTimeOffset.UtcNow.AddMinutes(5.1));
        var stored = Assert.Single(saved);
        Assert.Equal("svc:name:alice:k1", stored.Key);
        Assert.Equal(IdempotentResultState.Completed, stored.State);
        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Equal("""{"processId":"p-1"}""", stored.Response);
        Assert.InRange(stored.ExpiresUtc, DateTimeOffset.UtcNow.AddHours(1.9), DateTimeOffset.UtcNow.AddHours(2.1));
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
    public async Task AnAnswerWithoutAValue_IsNotStored()
    {
        var result = await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.NoContent()));

        Assert.IsType<NoContent>(result);
        Assert.Empty(saved);
        Assert.Equal("svc:name:alice:k1", Assert.Single(released));
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
    public async Task AnInformationalAnswer_IsNotStored()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Json(new { }, JsonSerializerOptions.Default, statusCode: StatusCodes.Status102Processing)));

        Assert.Empty(saved);
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
        Assert.Equal("svc:name:alice:k1", Assert.Single(released));
    }

    // #285: a retry while the first attempt still runs does not run; it is told to come back.
    [Fact]
    public async Task WithAKeyStillInProgress_Answers409WithRetryAfter_WithoutRunningTheEndpoint()
    {
        Holding(InProgress("svc:name:alice:k1", DateTimeOffset.UtcNow.AddMinutes(1)));
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
        Holding(InProgress("svc:name:alice:k1", DateTimeOffset.UtcNow.AddMinutes(-1)));
        var retry = Context("k1");

        var result = await CreateSut().InvokeAsync(retry, Endpoint);

        Assert.Equal(0, calls);
        var error = Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, Assert.Single(error.Value!.Errors).Code);
        Assert.False(retry.HttpContext.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task WithAKeyInProgressForADifferentRequest_Answers422()
    {
        Holding(InProgress("svc:name:alice:k1", DateTimeOffset.UtcNow.AddMinutes(1)) with { RequestHash = "other" });

        var result = await CreateSut().InvokeAsync(Context("k1"), Endpoint);

        Assert.Equal(0, calls);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(result).StatusCode);
    }

    [Fact]
    public async Task AnEndpointThatThrows_ReleasesTheKey_AndTheExceptionPropagates()
    {
        await Assert.ThrowsAsync<KaleidoValidationException>(() => CreateSut().InvokeAsync(
            Context("k1"),
            _ => throw new KaleidoValidationException("code", "bad")).AsTask());

        Assert.Empty(saved);
        Assert.Equal("svc:name:alice:k1", Assert.Single(released));
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

    private static IdempotentResult InProgress(string key, DateTimeOffset leaseUntilUtc) =>
        new()
        {
            Key = key,
            RequestHash = Sha256("POST /svc/processes/steps/start\n\n" + JsonSerializer.Serialize(new { step = "start" }, KaleidoJsonOptions.Options)),
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = leaseUntilUtc,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };
}