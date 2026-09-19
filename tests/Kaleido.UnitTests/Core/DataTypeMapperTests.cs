using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Kaleido.Abstractions.UnitTests;

public sealed class DataTypeMapperTests
{
    [Fact]
    public void GetDescriptor_WhenPropertyIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                DataTypeMapper.GetDescriptor((PropertyInfo)null!));

        Assert.Equal(
            "propertyInfo",
            exception.ParamName);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNullableValueType_PreservesUnderlyingDescriptorAndMarksNullable()
    {
        var descriptor = DataTypeMapper.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.NullableCount))!);

        Assert.Equal("integer", descriptor.Type);
        Assert.True(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNullableReferenceType_MarksNullable()
    {
        var descriptor = DataTypeMapper.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.NullableName))!);

        Assert.Equal("string", descriptor.Type);
        Assert.True(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNonNullableReferenceType_DoesNotMarkNullable()
    {
        var descriptor = DataTypeMapper.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.RequiredName))!);

        Assert.Equal("string", descriptor.Type);
        Assert.False(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsEnum_MapsEnumValuesAndDescriptions()
    {
        var descriptor = DataTypeMapper.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.Status))!);

        Assert.Equal("string", descriptor.Type);
        Assert.Equal("enum", descriptor.Format);

        Assert.Collection(
            descriptor.EnumValues!,
            active =>
            {
                Assert.Equal(1, active.Value);
                Assert.Equal(nameof(TestStatus.Active), active.Name);
                Assert.Equal("Currently active", active.Description);
            },
            inactive =>
            {
                Assert.Equal(2, inactive.Value);
                Assert.Equal(nameof(TestStatus.Inactive), inactive.Name);
                Assert.Null(inactive.Description);
            });
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsCollection_MapsArrayWithItemType()
    {
        var descriptor = DataTypeMapper.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.Ids))!);

        Assert.Equal("array", descriptor.Type);
        Assert.NotNull(descriptor.ItemType);
        Assert.Equal("string", descriptor.ItemType!.Type);
        Assert.Equal("uuid", descriptor.ItemType.Format);
    }

    [Fact]
    public void TryConvertValue_WhenValueIsJsonNumber_ConvertsToRequestedType()
    {
        using var document = JsonDocument.Parse("123");

        var result = DataTypeMapper.TryConvertValue(document.RootElement, typeof(int));

        Assert.True(result.Success);
        Assert.Equal(123, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenEnumTextMatches_ConvertsIgnoringCase()
    {
        var result = DataTypeMapper.TryConvertValue("active", typeof(TestStatus));

        Assert.True(result.Success);
        Assert.Equal(TestStatus.Active, Assert.IsType<TestStatus>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenValueIsInvalid_ReturnsFailure()
    {
        var result = DataTypeMapper.TryConvertValue("nope", typeof(int));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("'nope' is not a valid value for type 'Int32'.", result.ErrorMessage);
    }

    [Fact]
    public void TryConvertValue_WhenTargetTypeIsUnsupported_Throws()
    {
        var exception =
            Assert.Throws<UnsupportedDataTypeException>(() =>
                DataTypeMapper.TryConvertValue("{ }", typeof(TestObject)));

        Assert.Equal(typeof(TestObject), exception.DataType);
    }

    [Fact]
    public void ConvertValue_WhenConversionFails_ThrowsDataTypeConversionException()
    {
        var exception =
            Assert.Throws<DataTypeConversionException>(() =>
                DataTypeMapper.ConvertValue("bad-guid", typeof(Guid)));

        Assert.Equal("bad-guid", exception.Value);
        Assert.Equal(typeof(Guid), exception.TargetType);
        Assert.Equal("'bad-guid' is not a valid value for type 'Guid'.", exception.Message);
    }

    private enum TestStatus
    {
        [Description("Currently active")]
        Active = 1,
        Inactive = 2
    }

    private sealed class TestModel
    {
        public int? NullableCount { get; init; }

        public string? NullableName { get; init; }

        public string RequiredName { get; init; } = string.Empty;

        public TestStatus Status { get; init; }

        public List<Guid> Ids { get; init; } = [];
    }

    private sealed class TestObject
    {
    }
}
