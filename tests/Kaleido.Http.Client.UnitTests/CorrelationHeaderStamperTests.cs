using Kaleido.Observability;

namespace Kaleido.Http.Client.UnitTests;

public sealed class CorrelationHeaderStamperTests
    : Kaleido.UnitTests.SutFixture
{
    private static CorrelationHeaderStamper CreateSut(
        IKaleidoCorrelationContextAccessor correlation) =>
        new(correlation, new KaleidoServiceOptions { ServiceName = "intake" });

    // ---------------------------------------------------------------------------
    // Sanitize
    // ---------------------------------------------------------------------------

    [Fact]
    public void Sanitize_NullValue_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizerExtensions.Sanitize(null));
    }

    [Fact]
    public void Sanitize_WhiteSpaceOnly_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizerExtensions.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_PrintableAscii_ReturnsUnchanged()
    {
        Assert.Equal("hello-world_123", HttpHeaderSanitizerExtensions.Sanitize("hello-world_123"));
    }

    [Fact]
    public void Sanitize_ControlCharacters_AreStripped()
    {
        // \r, \n, \t, \0 are all control characters and must be stripped
        Assert.Equal("abc", HttpHeaderSanitizerExtensions.Sanitize("a\rb\nc\t"));
        Assert.Equal("abc", HttpHeaderSanitizerExtensions.Sanitize("a\0b\u001Fc"));
    }

    [Fact]
    public void Sanitize_NonAsciiCharacters_AreStripped()
    {
        Assert.Equal("caf", HttpHeaderSanitizerExtensions.Sanitize("caf\u00E9")); // é is non-ASCII
    }

    [Fact]
    public void Sanitize_LeadingAndTrailingSpaces_AreTrimmed()
    {
        Assert.Equal("trimmed", HttpHeaderSanitizerExtensions.Sanitize("  trimmed  "));
    }

    [Fact]
    public void Sanitize_ValueExceedingMaxLength_IsTruncated()
    {
        var longValue = new string('a', 300);
        var result = HttpHeaderSanitizerExtensions.Sanitize(longValue);
        Assert.NotNull(result);
        Assert.Equal(256, result!.Length);
    }

    [Fact]
    public void Sanitize_AfterStrippingBecomesEmpty_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizerExtensions.Sanitize("\r\n\t\0")); // all control chars, nothing left
    }

    // ---------------------------------------------------------------------------
    // Stamp
    // ---------------------------------------------------------------------------

    [Fact]
    public void Stamp_RequestIdPresent_AddsHeader()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { RequestId = "req-123" });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var values));
        Assert.Contains("req-123", values);
    }

    [Fact]
    public void Stamp_ProcessIdPresent_AddsHeader()
    {
        var id = Guid.NewGuid();
        var correlation = SetupContext(new KaleidoCorrelationContext { ProcessId = id });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values));
        Assert.Contains(id.ToString(), values);
    }

    [Fact]
    public void Stamp_InsideStep_AddsThisServiceAndExecutingStepAsCaller()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { ExecutingStepName = "route-to-radiology" });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.CallingProcessor, out var processor));
        Assert.Contains("intake", processor);
        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.CallingStep, out var step));
        Assert.Contains("route-to-radiology", step);
    }

    [Fact]
    public void Stamp_InboundCallerOutsideStep_IsNotForwarded()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext
        {
            CallingProcessorName = "router",
            CallingStepName = "upstream-step"
        });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public void Stamp_InboundCallerInsideStep_IsReplacedByThisService()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext
        {
            CallingProcessorName = "upstream",
            CallingStepName = "upstream-step",
            ExecutingStepName = "my-step"
        });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.Equal(["intake"], request.Headers.GetValues(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.Equal(["my-step"], request.Headers.GetValues(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public void Stamp_EmptyContext_AddsNoHeaders()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext());
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.CallingProcessor, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.CallingStep, out _));
    }

    [Fact]
    public void Stamp_RequestIdWithControlChars_SanitizesBeforeAdding()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { RequestId = "req\r\n123" });
        var stamper = CreateSut(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var values));
        Assert.Contains("req123", values);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IKaleidoCorrelationContextAccessor SetupContext(KaleidoCorrelationContext ctx)
    {
        var mock = new Mock<IKaleidoCorrelationContextAccessor>();
        mock.Setup(x => x.Current).Returns(ctx);
        return mock.Object;
    }
}
