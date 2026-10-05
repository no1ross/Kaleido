using Kaleido.Http.Observability;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Observability.UnitTests;

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
        Assert.Equal("X-Kaleido-Calling-Processor",    KaleidoCorrelationHeaders.CallingProcessor);
        Assert.Equal("X-Kaleido-Calling-Step",         KaleidoCorrelationHeaders.CallingStep);
    }

    [Fact]
    public void Read_MapsHeadersToCorrelationContext()
    {
        var processId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]           = "REQ-001";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId]           = processId.ToString();
        // the instance id is no longer read from the wire: an invalid value is not an error
        context.Request.Headers["X-Kaleido-Processor-Instance-Id"]       = "not-a-guid";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor]    = "intake";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingStep]         = "validate";

        var result = context.ReadCorrelationContext();

        Assert.Equal("REQ-001",  result.RequestId);
        Assert.Equal(processId,  result.ProcessId);
        Assert.Equal("intake",   result.CallingProcessorName);
        Assert.Equal("validate", result.CallingStepName);
    }

    [Fact]
    public void Read_WhenHeadersAreBlank_GeneratesRequestIdAndReturnsNullForOthers()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]           = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId]           = " ";
                context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor]    = " ";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingStep]         = " ";

        var result = context.ReadCorrelationContext();

        Assert.NotNull(result.RequestId);
        Assert.NotEmpty(result.RequestId);
        Assert.Null(result.ProcessId);
        Assert.Null(result.CallingProcessorName);
        Assert.Null(result.CallingStepName);
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
    public void Read_WhenIdentityNotTrusted_GeneratesRequestIdAndDropsIdentityFields()
    {
        var processId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]           = "REQ-001";
        context.Request.Headers[KaleidoCorrelationHeaders.ProcessId]           = processId.ToString();
        context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor]    = "intake";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingStep]         = "validate";

        var result = context.ReadCorrelationContext(trustIdentity: false);

        // ProcessId is a resumable handle — always honored.
        Assert.Equal(processId, result.ProcessId);

        // Identity fields are not honored; a fresh request id is generated.
        Assert.True(Guid.TryParse(result.RequestId, out _));
        Assert.NotEqual("REQ-001", result.RequestId);
        Assert.Null(result.CallingProcessorName);
        Assert.Null(result.CallingStepName);
    }

    [Fact]
    public void Read_StripsSanitizableCharsFromStringFields()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoCorrelationHeaders.RequestId]       = "REQ" + (char)0x00 + "001";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor] = "in" + (char)0x1f + "take";
        context.Request.Headers[KaleidoCorrelationHeaders.CallingStep]      = "vali" + (char)0x7f + "date";

        var result = context.ReadCorrelationContext();

        Assert.Equal("REQ001",   result.RequestId);
        Assert.Equal("intake",   result.CallingProcessorName);
        Assert.Equal("validate", result.CallingStepName);
    }
}
