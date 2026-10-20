using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Kaleido.Http.Client;

/// <summary>
/// Builds the resilience pipeline of a Kaleido client from <see cref="KaleidoResilienceOptions"/>: total
/// timeout, retry (safe requests only), circuit breaker, attempt timeout.
/// </summary>
internal static class KaleidoResiliencePipelineExtensions
{
    public static ResiliencePipelineBuilder<HttpResponseMessage> AddKaleidoResilience(
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
                // Retry-After (sent with "still running") sets the delay; ShouldRetryAfterHeader is on by default.
                ShouldHandle = args => ValueTask.FromResult(
                    args.Context.IsSafeToRetry()
                    && (HttpClientResiliencePredicates.IsTransient(args.Outcome) || args.Outcome.Result.IsStillRunning())),
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

    // The service's answer while the first request with the same Idempotency-Key is still running:
    // 409 with Retry-After (pro_idempotency_in_progress). The retry then gets the stored answer. Any
    // other 409 (a process conflict, an outcome that is unknown) has no Retry-After and is not retried.
    internal static bool IsStillRunning(this HttpResponseMessage? response) =>
        response is { StatusCode: System.Net.HttpStatusCode.Conflict, Headers.RetryAfter: not null }
        && response.RequestMessage?.Headers.Contains(KaleidoIdempotencyHeaders.IdempotencyKey) == true;

    // Reads, and requests the service answers again from its stored result (Idempotency-Key). Never the
    // health check, which must report quickly and honestly.
    internal static bool IsSafeToRetry(this ResilienceContext context)
    {
        var request = context.GetRequestMessage();

        if (request is null || (request.Options.TryGetValue(KaleidoResilienceOptions.NoRetry, out var noRetry) && noRetry))
        {
            return false;
        }

        return request.Method == HttpMethod.Get
            || request.Method == HttpMethod.Head
            || request.Headers.Contains(KaleidoIdempotencyHeaders.IdempotencyKey);
    }
}