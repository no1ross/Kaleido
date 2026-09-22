using Kaleido.Http.Client;
using Kaleido.Observability;

namespace Kaleido.Http.Client.Tests;

public sealed class CorrelationHeaderStamperTests
{
    // ---------------------------------------------------------------------------
    // Sanitize
    // ---------------------------------------------------------------------------

    [Fact]
    public void Sanitize_NullValue_ReturnsNull()
    {
        var stamper = CreateStamper();
        Assert.Null(stamper.Sanitize(null));
    }

    [Fact]
    public void Sanitize_WhiteSpaceOnly_ReturnsNull()
    {
        var stamper = CreateStamper();
        Assert.Null(stamper.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_PrintableAscii_ReturnsUnchanged()
    {
        var stamper = CreateStamper();
        Assert.Equal("hello-world_123", stamper.Sanitize("hello-world_123"));
    }

    [Fact]
    public void Sanitize_ControlCharacters_AreStripped()
    {
        var stamper = CreateStamper();
        // \r, \n, \t, \0 are all control characters and must be stripped
        Assert.Equal("abc", stamper.Sanitize("a\rb\nc\t"));
        Assert.Equal("abc", stamper.Sanitize("a\0b\u001Fc"));
    }

    [Fact]
    public void Sanitize_NonAsciiCharacters_AreStripped()
    {
        var stamper = CreateStamper();
        Assert.Equal("caf", stamper.Sanitize("caf\u00E9")); // é is non-ASCII
    }

    [Fact]
    public void Sanitize_LeadingAndTrailingSpaces_AreTrimmed()
    {
        var stamper = CreateStamper();
        Assert.Equal("trimmed", stamper.Sanitize("  trimmed  "));
    }

    [Fact]
    public void Sanitize_ValueExceedingMaxLength_IsTruncated()
    {
        var stamper = CreateStamper();
        var longValue = new string('a', 300);
        var result = stamper.Sanitize(longValue);
        Assert.NotNull(result);
        Assert.Equal(256, result!.Length);
    }

    [Fact]
    public void Sanitize_AfterStrippingBecomesEmpty_ReturnsNull()
    {
        var stamper = CreateStamper();
        Assert.Null(stamper.Sanitize("\r\n\t\0")); // all control chars, nothing left
    }

    // ---------------------------------------------------------------------------
    // Stamp
    // ---------------------------------------------------------------------------

    [Fact]
    public void Stamp_RequestIdPresent_AddsHeader()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { RequestId = "req-123" });
        var stamper = new CorrelationHeaderStamper(correlation);
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
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values));
        Assert.Contains(id.ToString(), values);
    }

    [Fact]
    public void Stamp_SourceProcessorNamePresent_AddsHeader()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { SourceProcessorName = "my-processor" });
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.SourceProcessor, out var values));
        Assert.Contains("my-processor", values);
    }

    [Fact]
    public void Stamp_ProcessorInstanceIdPresent_AddsHeader()
    {
        var id = Guid.NewGuid();
        var correlation = SetupContext(new KaleidoCorrelationContext { ProcessorInstanceId = id });
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessorInstanceId, out var values));
        Assert.Contains(id.ToString(), values);
    }

    [Fact]
    public void Stamp_StepNamePresent_AddsHeader()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { StepName = "my-step" });
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.StepName, out var values));
        Assert.Contains("my-step", values);
    }

    [Fact]
    public void Stamp_EmptyContext_AddsNoHeaders()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext());
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.SourceProcessor, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessorInstanceId, out _));
        Assert.False(request.Headers.TryGetValues(KaleidoCorrelationHeaders.StepName, out _));
    }

    [Fact]
    public void Stamp_RequestIdWithControlChars_SanitizesBeforeAdding()
    {
        var correlation = SetupContext(new KaleidoCorrelationContext { RequestId = "req\r\n123" });
        var stamper = new CorrelationHeaderStamper(correlation);
        var request = new HttpRequestMessage();

        stamper.Stamp(request);

        Assert.True(request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var values));
        Assert.Contains("req123", values);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static CorrelationHeaderStamper CreateStamper() =>
        new(SetupContext(new KaleidoCorrelationContext()));

    private static IKaleidoCorrelationContextAccessor SetupContext(KaleidoCorrelationContext ctx)
    {
        var mock = new Mock<IKaleidoCorrelationContextAccessor>();
        mock.Setup(x => x.Current).Returns(ctx);
        return mock.Object;
    }
}
