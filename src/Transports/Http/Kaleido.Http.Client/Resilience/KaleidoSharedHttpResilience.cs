using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Kaleido.Http.Client.Resilience;

internal static class KaleidoSharedHttpResilience
{
    internal static ResiliencePipelineBuilder<HttpResponseMessage> AddSafeResilience(
        this ResiliencePipelineBuilder<HttpResponseMessage> pipeline,
        KaleidoResilienceOptions options)
    {
        pipeline.AddTimeout(options.TotalTimeout);
        if (options.MaxRetryAttempts > 0)
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Context.GetRequestMessage() is { } request
                    && !(request.Options.TryGetValue(KaleidoResilienceOptions.NoRetry, out var noRetry) && noRetry)
                    && (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
                    && HttpClientResiliencePredicates.IsTransient(args.Outcome)),
            });
        }

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = options.CircuitBreakerFailureRatio,
            MinimumThroughput = options.CircuitBreakerMinimumThroughput,
            SamplingDuration = options.CircuitBreakerSamplingDuration,
            BreakDuration = options.CircuitBreakerBreakDuration,
        });
        return pipeline.AddTimeout(options.AttemptTimeout);
    }
}
