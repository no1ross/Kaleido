using Kaleido.UnitTests;

namespace Kaleido.Exceptions.UnitTests;

public sealed class KaleidoValidationExceptionTests : SutFixture<KaleidoValidationException>
{
    protected override KaleidoValidationException CreateSut() => new("test_invalid_field", "Field 'test-field' does not exist.");

    [Fact]
    public void Constructor_PreservesCodeAndMessage()
    {
        var exception = Sut;

        Assert.Equal("test_invalid_field", exception.Code);
        Assert.Equal("Field 'test-field' does not exist.", exception.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesIt()
    {
        var inner = new InvalidOperationException("inner");
        var exception = CreateSut(inner);

        Assert.Equal("test_invalid_filter_value", exception.Code);
        Assert.Equal("Bad filter value.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    private static KaleidoValidationException CreateSut(Exception inner) => new("test_invalid_filter_value", "Bad filter value.", inner);
}
