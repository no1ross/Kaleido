namespace Kaleido.Http.UnitTests.Abstractions;

public sealed class HttpHeaderSanitizerTests
{
    [Fact]
    public void Sanitize_WhenNull_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizer.Sanitize(null));
    }

    [Fact]
    public void Sanitize_WhenEmpty_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizer.Sanitize(string.Empty));
    }

    [Fact]
    public void Sanitize_WhenWhitespaceOnly_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizer.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_PrintableAscii_PassesThroughUnchanged()
    {
        var value = "REQ-001 hello/world~";
        Assert.Equal(value, HttpHeaderSanitizer.Sanitize(value));
    }

    [Fact]
    public void Sanitize_StripsControlCharacters()
    {
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u0000lo"));
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u001flo"));
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u007flo"));
    }

    [Fact]
    public void Sanitize_StripsHighUnicode()
    {
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u0080lo"));
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u00FFlo"));
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("hel\u4E2Dlo"));
    }

    [Fact]
    public void Sanitize_Trims_LeadingAndTrailingWhitespace()
    {
        Assert.Equal("hello", HttpHeaderSanitizer.Sanitize("  hello  "));
    }

    [Fact]
    public void Sanitize_WhenAllCharsStripped_ReturnsNull()
    {
        Assert.Null(HttpHeaderSanitizer.Sanitize("\u0000\u001f\u007f"));
    }

    [Fact]
    public void Sanitize_TruncatesAtMaxLength()
    {
        var longValue = new string('a', HttpHeaderSanitizer.MaxLength + 50);
        var result = HttpHeaderSanitizer.Sanitize(longValue);

        Assert.NotNull(result);
        Assert.Equal(HttpHeaderSanitizer.MaxLength, result!.Length);
    }

    [Fact]
    public void Sanitize_ExactlyMaxLength_IsNotTruncated()
    {
        var value = new string('a', HttpHeaderSanitizer.MaxLength);
        Assert.Equal(value, HttpHeaderSanitizer.Sanitize(value));
    }

    [Fact]
    public void Sanitize_MixedContent_StripsBadCharsAndTruncates()
    {
        // control chars interleaved, result should be clean printable chars up to MaxLength
        var dirty = new string('x', 100) + "\u0000" + new string('y', 200);
        var result = HttpHeaderSanitizer.Sanitize(dirty);

        Assert.NotNull(result);
        Assert.Equal(HttpHeaderSanitizer.MaxLength, result!.Length);
        Assert.All(result!, c => Assert.True(c >= 0x20 && c <= 0x7E));
    }
}
