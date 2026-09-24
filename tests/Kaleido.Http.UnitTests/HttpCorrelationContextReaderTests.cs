using Kaleido.Http.Observability;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.UnitTests;

public sealed class HttpCorrelationContextReaderTests
    : SutFixture
{
    [Fact]
    public void Read_WhenContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((HttpContext)null!).ReadCorrelationContext());
    }

    [Fact]
    public void HeaderConstants_UseExpectedNames()
    {
        Assert.Equal("X-Kaleido-Request-Id",           KaleidoCorrelationHeaders.RequestId);
        Assert.Equal("X-Kaleido-Process-Id",           KaleidoCorrelationHeaders.ProcessId);
        Assert.Equal("X-Kaleido-Processor-Instance-Id", KaleidoCorrelationHeaders.ProcessorInstanceId);
        Assert.Equal("X-Kaleido-Source-Processor",     KaleidoCorrelationHeaders.SourceProcessor);
        Assert.Equal("X-Kaleido-Step-Name",            KaleidoCorrelationHeaders.StepName);
    }

    [Fact]
    public void Read_MapsHeadersToCorrelationContext()
    {
        var processId = Guid.NewGuid();
        var processorInstanceId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]           = "REQ-001";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId]           = processId.ToString();
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = processorInstanceId.ToString();
        context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor]     = "intake";
        context.Request.Headers[KaleidoCorrelationHeaders.StepName]            = "validate";

        var result = context.ReadCorrelationContext();

        Assert.Equal("REQ-001",  result.RequestId);
        Assert.Equal(processId,  result.ProcessId);
        Assert.Equal(processorInstanceId, result.ProcessorInstanceId);
        Assert.Equal("intake",   result.SourceProcessorName);
        Assert.Equal("validate", result.StepName);
    }

    [Fact]
    public void Read_WhenHeadersAreBlank_GeneratesRequestIdAndReturnsNullForOthers()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]           = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId]           = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor]     = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.StepName]            = " ";

        var result = context.ReadCorrelationContext();

        Assert.NotNull(result.RequestId);
        Assert.NotEmpty(result.RequestId);
        Assert.Null(result.ProcessId);
        Assert.Null(result.ProcessorInstanceId);
        Assert.Null(result.SourceProcessorName);
        Assert.Null(result.StepName);
    }

    [Fact]
    public void Read_WhenGuidHeaderIsInvalid_ThrowsBadHttpRequestException()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = "not-a-guid";

        var exception = Assert.Throws<BadHttpRequestException>(() =>
            context.ReadCorrelationContext());

        Assert.Contains(KaleidoCorrelationHeaders.ProcessId, exception.Message);
    }

    [Fact]
    public void Read_WhenRequestIdHeaderAbsent_GeneratesNewRequestId()
    {
        var context = new DefaultHttpContext();

        var result = context.ReadCorrelationContext();

        Assert.NotNull(result.RequestId);
        Assert.True(Guid.TryParse(result.RequestId, out _));
    }

    [Fact]
    public void Read_StripsSanitizableCharsFromStringFields()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]       = "REQ" + (char)0x00 + "001";
        context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor] = "in" + (char)0x1f + "take";
        context.Request.Headers[KaleidoCorrelationHeaders.StepName]        = "vali" + (char)0x7f + "date";

        var result = context.ReadCorrelationContext();

        Assert.Equal("REQ001",   result.RequestId);
        Assert.Equal("intake",   result.SourceProcessorName);
        Assert.Equal("validate", result.StepName);
    }
}
