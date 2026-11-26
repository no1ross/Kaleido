using Kaleido.Http.Client.Resilience;
using Microsoft.Extensions.Time.Testing;
using Polly;

namespace Kaleido.Process.Http.Client.UnitTests;

public sealed class ProcessHttpKeyedResilienceTests : Kaleido.UnitTests.SutFixture
{
    private static ResiliencePipelineBuilder<HttpResponseMessage> CreateSut(FakeTimeProvider time) =>
        new() { TimeProvider = time };

    [Fact]
    public void AddKeyedPostRetry_WhenRetriesAreDisabled_BuildsWithoutARetryStrategy()
    {
        var pipeline = CreateSut(new FakeTimeProvider());

        Assert.Same(pipeline, pipeline.AddKeyedPostRetry(new KaleidoResilienceOptions { MaxRetryAttempts = 0 }));
        Assert.NotNull(pipeline.Build());
    }

    [Theory]
    [InlineData("POST", "logical-call-key", HttpStatusCode.ServiceUnavailable, 2)]
    [InlineData("POST", null, HttpStatusCode.ServiceUnavailable, 1)]
    [InlineData("GET", "logical-call-key", HttpStatusCode.ServiceUnavailable, 1)]
    [InlineData("POST", " ", HttpStatusCode.ServiceUnavailable, 1)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.Conflict, 1)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.UnprocessableEntity, 1)]
    [InlineData(null, "logical-call-key", HttpStatusCode.ServiceUnavailable, 1)]
    public async Task AddKeyedPostRetry_OnlyRetriesTransientPostWithOneStableKey(string? method, string? key, HttpStatusCode status, int expectedAttempts) =>
        Assert.Equal(expectedAttempts, await AttemptsAsync(method, key, status));

    [Theory]
    [InlineData("POST", "logical-call-key", HttpStatusCode.Conflict, true, false, 2)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.Conflict, false, false, 1)]
    [InlineData("POST", null, HttpStatusCode.Conflict, true, false, 1)]
    [InlineData("POST", " ", HttpStatusCode.Conflict, true, false, 1)]
    [InlineData("GET", "logical-call-key", HttpStatusCode.Conflict, true, false, 1)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.UnprocessableEntity, true, false, 1)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.Conflict, true, true, 1)]
    [InlineData("POST", "logical-call-key", HttpStatusCode.ServiceUnavailable, false, true, 1)]
    [InlineData(null, "logical-call-key", HttpStatusCode.Conflict, true, false, 1)]
    public async Task AddKeyedPostRetry_OnlyRetriesInProgress409WithOneKeyAndWithoutNoRetry(
        string? method, string? key, HttpStatusCode status, bool retryAfter, bool noRetry, int expectedAttempts) =>
        Assert.Equal(expectedAttempts, await AttemptsAsync(method, key, status, retryAfter, noRetry));

    [Fact]
    public async Task AddKeyedPostRetry_UsesTheRetryAfterBoundaryInsteadOfTheSharedDelay()
    {
        var time = new FakeTimeProvider();
        var pipeline = CreateSut(time).AddKeyedPostRetry(new KaleidoResilienceOptions
        {
            MaxRetryAttempts = 1,
            RetryDelay = TimeSpan.FromMilliseconds(1),
        }).Build();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/execute");
        request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, "logical-call-key");
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        context.SetRequestMessage(request);
        var attempts = 0;
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var pending = pipeline.ExecuteOutcomeAsync((_, _) =>
            {
                var response = new HttpResponseMessage(Interlocked.Increment(ref attempts) == 1 ? HttpStatusCode.Conflict : HttpStatusCode.OK)
                {
                    RequestMessage = request,
                };
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                    first.TrySetResult();
                }
                else
                {
                    second.TrySetResult();
                }

                return ValueTask.FromResult(Outcome.FromResult(response));
            }, context, state: 0).AsTask();
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            for (var spin = 0; spin < 20; spin++)
            {
                await Task.Yield();
            }

            Assert.False(pending.IsCompleted);
            time.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromTicks(1));
            Assert.Equal(1, Volatile.Read(ref attempts));
            time.Advance(TimeSpan.FromTicks(1));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(2, attempts);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private static async Task<int> AttemptsAsync(string? method, string? key, HttpStatusCode status, bool retryAfter = false, bool noRetry = false)
    {
        var time = new FakeTimeProvider();
        var pipeline = CreateSut(time).AddKeyedPostRetry(new KaleidoResilienceOptions
        {
            MaxRetryAttempts = 1,
            RetryDelay = TimeSpan.FromSeconds(1),
        }).Build();
        using var request = method is null ? null : new HttpRequestMessage(new HttpMethod(method), "/catalog/1/processes/execute");
        if (key is not null && request is not null)
        {
            request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        }

        if (noRetry && request is not null)
        {
            request.Options.Set(KaleidoResilienceOptions.NoRetry, value: true);
        }

        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        if (request is not null)
        {
            context.SetRequestMessage(request);
        }
        var attempts = 0;
        try
        {
            var pending = pipeline.ExecuteOutcomeAsync((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                var response = new HttpResponseMessage(status) { RequestMessage = request };
                if (retryAfter)
                {
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                }

                return ValueTask.FromResult(Outcome.FromResult(response));
            }, context, state: 0).AsTask();
            for (var tick = 0; tick < 5 && !pending.IsCompleted; tick++)
            {
                await Task.Yield();
                time.Advance(TimeSpan.FromSeconds(2));
            }

            await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }

        return attempts;
    }
}
