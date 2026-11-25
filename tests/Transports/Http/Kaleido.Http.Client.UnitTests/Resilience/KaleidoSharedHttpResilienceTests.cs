using Microsoft.Extensions.Time.Testing;
using Polly;

namespace Kaleido.Http.Client.Resilience.UnitTests;

public sealed class KaleidoSharedHttpResilienceTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddSafeResilience_BuildsATypedHttpPipeline()
    {
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>();

        Assert.Same(pipeline, pipeline.AddSafeResilience(new KaleidoResilienceOptions { MaxRetryAttempts = 0 }));
        Assert.NotNull(pipeline.Build());
    }

    [Theory]
    [InlineData("GET", HttpStatusCode.ServiceUnavailable, null, 2)]
    [InlineData("GET", HttpStatusCode.ServiceUnavailable, false, 2)]
    [InlineData("HEAD", HttpStatusCode.ServiceUnavailable, null, 2)]
    [InlineData("GET", HttpStatusCode.BadRequest, false, 1)]
    [InlineData("POST", HttpStatusCode.ServiceUnavailable, false, 1)]
    [InlineData("GET", HttpStatusCode.ServiceUnavailable, true, 1)]
    [InlineData("GET", HttpStatusCode.Conflict, false, 1)]
    [InlineData(null, HttpStatusCode.ServiceUnavailable, null, 1)]
    public async Task AddSafeResilience_RetriesOnlyTransientReadsUnlessNoRetryIsSet(string? method, HttpStatusCode status, bool? noRetry, int expectedAttempts)
    {
        var time = new FakeTimeProvider();
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = time }
            .AddSafeResilience(new KaleidoResilienceOptions { MaxRetryAttempts = 1, RetryDelay = TimeSpan.FromSeconds(1) }).Build();
        using var request = method is null ? null : new HttpRequestMessage(new HttpMethod(method), "/catalog/1/processes/state");
        if (noRetry is { } value && request is not null)
        {
            request.Options.Set(KaleidoResilienceOptions.NoRetry, value);
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
