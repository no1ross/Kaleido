using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Kaleido.Process.Http.Client;

internal static class ProcessHttpKeyedResilience
{
    internal static ResiliencePipelineBuilder<HttpResponseMessage> AddKeyedPostRetry(
        this ResiliencePipelineBuilder<HttpResponseMessage> pipeline,
        KaleidoResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxRetryAttempts == 0)
        {
            return pipeline;
        }

        return pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            Delay = options.RetryDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = args => ValueTask.FromResult(
                args.Context.GetRequestMessage() is { } request
                && request.Method == HttpMethod.Post
                && HasOneKey(request)
                && !(request.Options.TryGetValue(KaleidoResilienceOptions.NoRetry, out var noRetry) && noRetry)
                && (HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    || args.Outcome.Result is { StatusCode: System.Net.HttpStatusCode.Conflict, Headers.RetryAfter: not null })),
        });
    }

    private static bool HasOneKey(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues(KaleidoIdempotencyHeaders.IdempotencyKey, out var values))
        {
            return false;
        }

        var keys = values.Take(2).ToArray();
        return keys.Length == 1 && !string.IsNullOrWhiteSpace(keys[0]);
    }
}
