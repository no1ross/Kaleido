using System.Text.Json;
using Kaleido.Json;

namespace Kaleido.UnitTests;

public sealed class ValueConverterTests
{
    private readonly ValueConverter _sut = new();

    [Fact]
    public void Convert_WhenTargetTypeIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                _sut.Convert(
                    "value",
                    null!));

        Assert.Equal(
            "targetType",
            exception.ParamName);
    }

    [Fact]
    public void Convert_WhenValueIsNull_ReturnsNull()
    {
        var result =
            _sut.Convert(
                null,
                typeof(string));

        Assert.Null(result);
    }

    [Fact]
    public void Convert_WhenTargetTypeIsEnum_ParsesIgnoringCase()
    {
        var result =
            _sut.Convert(
                "active",
                typeof(TestStatus));

        Assert.Equal(
            TestStatus.Active,
            Assert.IsType<TestStatus>(result));
    }

    [Fact]
    public void Convert_WhenValueIsJsonElement_ConvertsToRequestedPrimitive()
    {
        using var document = JsonDocument.Parse("123");

        var result =
            _sut.Convert(
                document.RootElement,
                typeof(int));

        Assert.Equal(123, Assert.IsType<int>(result));
    }

    [Fact]
    public void Convert_WhenValueIsJsonElement_ConvertsToRequestedObject()
    {
        using var document = JsonDocument.Parse("{\"Name\":\"Alice\"}");

        var result =
            _sut.Convert(
                document.RootElement,
                typeof(TestPayload));

        var payload = Assert.IsType<TestPayload>(result);

        Assert.Equal(
            "Alice",
            payload.Name);
    }

    [Fact]
    public void Convert_WhenTargetTypeIsNullable_UsesUnderlyingType()
    {
        var result =
            _sut.Convert(
                "42",
                typeof(int?));

        Assert.Equal(42, Assert.IsType<int>(result));
    }

    private enum TestStatus
    {
        Active,
        Inactive
    }

    private sealed class TestPayload
    {
        public string? Name { get; init; }
    }
}
