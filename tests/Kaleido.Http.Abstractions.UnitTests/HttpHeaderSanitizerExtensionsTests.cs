namespace Kaleido.Http.UnitTests;

public sealed class HttpHeaderSanitizerExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Sanitize_WhenInputIsNullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(input.Sanitize());
    }

    [Fact]
    public void Sanitize_WhenValueIsPrintableAndUnpadded_ReturnsSameInstance()
    {
        var input = new string("hello world".ToCharArray());

        Assert.Same(input, input.Sanitize());
    }

    [Fact]
    public void Sanitize_WhenValueHasPadding_ReturnsTrimmedValue()
    {
        Assert.Equal("hello", "  hello  ".Sanitize());
    }

    [Theory]
    [InlineData("bad\x00value", "badvalue")]
    [InlineData("bad\x1Fvalue", "badvalue")]
    [InlineData("caf\u00E9", "caf")]
    [InlineData("x\x7Fy", "xy")]
    public void Sanitize_WhenValueContainsNonPrintableChars_StripsThem(
        string input,
        string expected)
    {
        Assert.Equal(expected, input.Sanitize());
    }

    [Fact]
    public void Sanitize_WhenValueIsAllNonPrintable_ReturnsNull()
    {
        Assert.Null("\x00\x01\x1F".Sanitize());
    }

    [Fact]
    public void Sanitize_WhenValueExceedsMaxLength_Truncates()
    {
        var input = new string('a', HttpHeaderSanitizerExtensions.MaxLength + 100);

        var result = input.Sanitize();

        Assert.NotNull(result);
        Assert.Equal(HttpHeaderSanitizerExtensions.MaxLength, result.Length);
    }

    [Fact]
    public void Sanitize_WhenValueIsExactlyMaxLength_KeepsAll()
    {
        var input = new string('a', HttpHeaderSanitizerExtensions.MaxLength);

        Assert.Same(input, input.Sanitize());
    }
}
