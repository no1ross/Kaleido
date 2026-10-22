using Polly;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoResiliencePipelineExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoResilienceOptions Fast = new() { MaxRetryAttempts = 2, RetryDelay = TimeSpan.FromMilliseconds(1) };

    private static async Task<int> AttemptsAsync(HttpRequestMessage request, HttpStatusCode answer, KaleidoResilienceOptions? options = null, TimeSpan? retryAfter = null)
    {
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>().AddKaleidoResilience(options ?? Fast).Build();
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        context.SetRequestMessage(request);
        var attempts = 0;

        try
        {
            await pipeline.ExecuteOutcomeAsync(
                (_, _) =>
                {
                    attempts++;
                    var response = new HttpResponseMessage(answer) { RequestMessage = request };

                    if (retryAfter is { } delay)
                    {
                        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
                    }

                    return ValueTask.FromResult(Outcome.FromResult(response));
                },
                context,
                state: 0);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }

        return attempts;
    }

    [Fact]
    public async Task AReadThatFailsTransiently_IsRetried()
    {
        Assert.Equal(3, await AttemptsAsync(new HttpRequestMessage(HttpMethod.Get, "/svc/registry"), HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task AnExecuteCallWithAnIdempotencyKey_IsRetried()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/svc/processes/execute");
        request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, "k1");

        Assert.Equal(3, await AttemptsAsync(request, HttpStatusCode.ServiceUnavailable));
    }

    // Retrying it could run the step twice.
    [Fact]
    public async Task AWriteWithoutAnIdempotencyKey_IsNeverRetried()
    {
        Assert.Equal(1, await AttemptsAsync(new HttpRequestMessage(HttpMethod.Post, "/svc/processes/execute"), HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task ARequestMarkedNoRetry_IsNeverRetried()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/svc/registry");
        request.Options.Set(KaleidoResilienceOptions.NoRetry, value: true);

        Assert.Equal(1, await AttemptsAsync(request, HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task ARequestWithNoRetrySetToFalse_IsRetried()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/svc/registry");
        request.Options.Set(KaleidoResilienceOptions.NoRetry, value: false);

        Assert.Equal(3, await AttemptsAsync(request, HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task AHeadThatFailsTransiently_IsRetried()
    {
        Assert.Equal(3, await AttemptsAsync(new HttpRequestMessage(HttpMethod.Head, "/svc/registry"), HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public void IsSafeToRetry_WithoutARequestMessage_IsFalse()
    {
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);

        try
        {
            Assert.False(context.IsSafeToRetry());
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    // #285: the service answers 409 + Retry-After while the first request with the key still runs;
    // the client waits and asks again, and then gets the stored answer.
    [Fact]
    public async Task AKeyedCallAnsweredStillRunning_IsRetried()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/svc/processes/execute");
        request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, "k1");

        Assert.Equal(3, await AttemptsAsync(request, HttpStatusCode.Conflict, retryAfter: TimeSpan.Zero));
    }

    // A process conflict, or an outcome that is unknown, comes without Retry-After: retrying would not help.
    [Fact]
    public async Task AKeyedCallAnsweredConflictWithoutRetryAfter_IsNotRetried()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/svc/processes/execute");
        request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, "k1");

        Assert.Equal(1, await AttemptsAsync(request, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task AReadAnsweredConflictWithRetryAfter_IsNotRetried()
    {
        Assert.Equal(1, await AttemptsAsync(new HttpRequestMessage(HttpMethod.Get, "/svc/registry"), HttpStatusCode.Conflict, retryAfter: TimeSpan.Zero));
    }

    [Fact]
    public async Task ANonTransientFailure_IsNotRetried()
    {
        Assert.Equal(1, await AttemptsAsync(new HttpRequestMessage(HttpMethod.Get, "/svc/registry"), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task WithZeroRetryAttempts_NothingIsRetried()
    {
        Assert.Equal(1, await AttemptsAsync(
            new HttpRequestMessage(HttpMethod.Get, "/svc/registry"),
            HttpStatusCode.ServiceUnavailable,
            new KaleidoResilienceOptions { MaxRetryAttempts = 0 }));
    }
}