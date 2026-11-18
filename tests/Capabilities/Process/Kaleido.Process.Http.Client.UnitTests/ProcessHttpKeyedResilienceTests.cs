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
    public async Task AddKeyedPostRetry_OnlyRetriesTransientPostWithOneStableKey(string method, string? key, HttpStatusCode status, int expectedAttempts)
    {
        var time = new FakeTimeProvider();
        var pipeline = CreateSut(time).AddKeyedPostRetry(new KaleidoResilienceOptions
        {
            MaxRetryAttempts = 1,
            RetryDelay = TimeSpan.FromSeconds(1),
        }).Build();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/catalog/1/processes/execute");
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        }

        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        context.SetRequestMessage(request);
        var attempts = 0;
        try
        {
            var pending = pipeline.ExecuteOutcomeAsync((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                return ValueTask.FromResult(Outcome.FromResult(new HttpResponseMessage(status) { RequestMessage = request }));
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

        Assert.Equal(expectedAttempts, attempts);
    }
}
