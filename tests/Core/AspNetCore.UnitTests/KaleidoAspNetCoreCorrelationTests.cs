using Kaleido.AspNetCore.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;

namespace Kaleido.AspNetCore.UnitTests;

public sealed class KaleidoAspNetCoreCorrelationTests
{
    [Fact]
    public void Create_WhenContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KaleidoAspNetCoreCorrelation.Create(null!));
    }

    [Fact]
    public void HeaderConstants_UseExpectedNames()
    {
        Assert.Equal("X-Kaleido-Request-Id", KaleidoCorrelationHeaders.RequestId);
        Assert.Equal("X-Kaleido-Process-Id", KaleidoCorrelationHeaders.ProcessId);
        Assert.Equal("X-Kaleido-Processor-Instance-Id", KaleidoCorrelationHeaders.ProcessorInstanceId);
        Assert.Equal("X-Kaleido-Source-Processor", KaleidoCorrelationHeaders.SourceProcessor);
    }

    [Fact]
    public void Create_MapsHeadersToCorrelationContext()
    {
        var processId = Guid.NewGuid();
        var processorInstanceId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId] = "REQ-001";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = processId.ToString();
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = processorInstanceId.ToString();
        context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor] = "intake";

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Equal("REQ-001", result.RequestId);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(processorInstanceId, result.ProcessorInstanceId);
        Assert.Equal("intake", result.SourceProcessorName);
    }

    [Fact]
    public void Create_WhenHeadersAreBlank_ReturnsNullValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId] = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor] = " ";

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Null(result.RequestId);
        Assert.Null(result.ProcessId);
        Assert.Null(result.ProcessorInstanceId);
        Assert.Null(result.SourceProcessorName);
    }

    [Fact]
    public void Create_WhenGuidHeaderIsInvalid_ThrowsBadHttpRequestException()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = "not-a-guid";

        var exception = Assert.Throws<BadHttpRequestException>(() =>
            KaleidoAspNetCoreCorrelation.Create(context));

        Assert.Contains(
            KaleidoCorrelationHeaders.ProcessId,
            exception.Message);
    }
}
