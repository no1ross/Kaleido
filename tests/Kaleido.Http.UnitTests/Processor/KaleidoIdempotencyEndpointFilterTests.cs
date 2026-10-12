using System.Text.Json;
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
    private readonly List<IdempotentResult> saved = [];
    private int calls;

    public KaleidoIdempotencyEndpointFilterTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerName = "alice" });
        store.Setup(x => x.SaveResultAsync(It.IsAny<IdempotentResult>(), It.IsAny<CancellationToken>()))
            .Callback<IdempotentResult, CancellationToken>((result, _) => saved.Add(result))
            .Returns(Task.CompletedTask);
    }

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
        var stored = Assert.Single(saved);
        Assert.Equal("svc:alice:k1", stored.Key);
        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Equal("""{"processId":"p-1"}""", stored.Response);
        Assert.InRange(stored.ExpiresUtc, DateTimeOffset.UtcNow.AddHours(1.9), DateTimeOffset.UtcNow.AddHours(2.1));
    }

    [Fact]
    public async Task WithAStoredKeyAndTheSameRequest_ReplaysTheAnswerWithoutRunningTheEndpoint()
    {
        await CreateSut().InvokeAsync(Context("k1"), Endpoint);
        store.Setup(x => x.FindResultAsync("svc:alice:k1", It.IsAny<CancellationToken>())).ReturnsAsync(saved[0]);
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
        store.Setup(x => x.FindResultAsync("svc:alice:k1", It.IsAny<CancellationToken>())).ReturnsAsync(saved[0]);

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
    public async Task AFailedAnswer_IsNotStored()
    {
        await CreateSut().InvokeAsync(
            Context("k1"),
            _ => ValueTask.FromResult<object?>(Results.Json(new { error = "no" }, JsonSerializerOptions.Default, statusCode: StatusCodes.Status400BadRequest)));

        Assert.Empty(saved);
    }
}