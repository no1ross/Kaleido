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
}
