using Kaleido.Json;
using System.Text.Json;

namespace Kaleido.UnitTests;

public sealed class ValueConverterTests
{
    [Fact]
    public void Convert_WhenTargetTypeIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                ValueConverter.Convert(
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
            ValueConverter.Convert(
                null,
                typeof(string));

        Assert.Null(result);
    }

    [Fact]
    public void Convert_WhenTargetTypeIsEnum_ParsesIgnoringCase()
    {
        var result =
            ValueConverter.Convert(
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
            ValueConverter.Convert(
                document.RootElement,
                typeof(int));

        Assert.Equal(123, Assert.IsType<int>(result));
    }

    [Fact]
    public void Convert_WhenValueIsJsonElement_ConvertsToRequestedObject()
    {
        using var document = JsonDocument.Parse("{\"Name\":\"Alice\"}");

        var result =
            ValueConverter.Convert(
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
            ValueConverter.Convert(
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
