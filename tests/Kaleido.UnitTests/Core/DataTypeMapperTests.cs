using System.ComponentModel;
using System.Text.Json;
using Kaleido.Exceptions;

namespace Kaleido.Abstractions.UnitTests;

public sealed class DataTypeMapperTests
    : Kaleido.UnitTests.SutFixture
{
    private static DataTypeMapper CreateSut() =>
        new();

    private readonly DataTypeMapper _sut = CreateSut();

    [Fact]
    public void GetDescriptor_WhenPropertyIsNullableValueType_PreservesUnderlyingDescriptorAndMarksNullable()
    {
        var descriptor = _sut.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.NullableCount))!);

        Assert.Equal("integer", descriptor.Type);
        Assert.True(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNullableReferenceType_MarksNullable()
    {
        var descriptor = _sut.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.NullableName))!);

        Assert.Equal("string", descriptor.Type);
        Assert.True(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNonNullableReferenceType_DoesNotMarkNullable()
    {
        var descriptor = _sut.GetDescriptor(
            typeof(TestModel).GetProperty(nameof(TestModel.RequiredName))!);

        Assert.Equal("string", descriptor.Type);
        Assert.False(descriptor.Nullable);
        Assert.Null(descriptor.Format);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsEnum_MapsEnumValuesAndDescriptions()
    {
        var descriptor = _sut.GetDescriptor(
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
        var descriptor = _sut.GetDescriptor(
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

        var result = _sut.TryConvertValue(document.RootElement, typeof(int));

        Assert.True(result.Success);
        Assert.Equal(123, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenEnumTextMatches_ConvertsIgnoringCase()
    {
        var result = _sut.TryConvertValue("active", typeof(TestStatus));

        Assert.True(result.Success);
        Assert.Equal(TestStatus.Active, Assert.IsType<TestStatus>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenValueIsInvalid_ReturnsFailure()
    {
        var result = _sut.TryConvertValue("nope", typeof(int));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("'nope' is not a valid value for type 'Int32'.", result.ErrorMessage);
    }

    [Fact]
    public void TryConvertValue_WhenTargetTypeIsUnsupported_Throws()
    {
        var exception =
            Assert.Throws<KaleidoFrameworkException>(() =>
                _sut.TryConvertValue("{ }", typeof(TestObject)));

        Assert.Equal(FrameworkErrorCodes.UnsupportedDataType, exception.Code);
        Assert.Contains("TestObject", exception.Message);
    }

    [Fact]
    public void ConvertValue_WhenConversionFails_ThrowsKaleidoFrameworkException()
    {
        var exception =
            Assert.Throws<KaleidoFrameworkException>(() =>
                _sut.ConvertValue<Guid>("bad-guid"));

        Assert.Equal(FrameworkErrorCodes.DataConversionError, exception.Code);
        Assert.Contains("bad-guid", exception.Message);
        Assert.Contains("Guid", exception.Message);
    }

    [Fact]
    public void GetDescriptor_WhenEnumWithDescription_MapsEnumValues()
    {
        // Tests reflection safety for GetMember array access
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Status))!);

        Assert.Equal("string", descriptor.Type);
        Assert.Equal("enum", descriptor.Format);
        Assert.NotNull(descriptor.EnumValues);
        Assert.Equal(2, descriptor.EnumValues.Count);
    }

    [Fact]
    public void GetDescriptor_WhenArrayType_MapsWithItemType()
    {
        // Tests reflection safety for GetElementType null check
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Ids))!);

        Assert.Equal("array", descriptor.Type);
        Assert.NotNull(descriptor.ItemType);
        Assert.Equal("string", descriptor.ItemType!.Type);
    }

    [Fact]
    public void GetDescriptor_WhenGenericList_MapsWithItemType()
    {
        // Tests reflection safety for GetGenericArguments array access
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Names))!);

        Assert.Equal("array", descriptor.Type);
        Assert.NotNull(descriptor.ItemType);
        Assert.Equal("string", descriptor.ItemType!.Type);
    }

    [Fact]
    public void TryConvertValue_Generic_WhenConversionSucceeds_ReturnsTypedResult()
    {
        var result = _sut.TryConvertValue<int>("123");

        Assert.True(result.Success);
        Assert.Equal(123, result.Value);
    }

    [Fact]
    public void TryConvertValue_Generic_WhenConversionFails_ReturnsFailedResult()
    {
        var result = _sut.TryConvertValue<int>("not-a-number");

        Assert.False(result.Success);
        Assert.Equal(default, result.Value);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void TryConvertValue_Generic_WhenNullAndNullableType_ReturnsNull()
    {
        var result = _sut.TryConvertValue<int?>(null);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryConvertValue_Generic_WhenNullAndNonNullableType_ReturnsFailure()
    {
        var result = _sut.TryConvertValue<int>(null);

        Assert.False(result.Success);
        Assert.Equal(default, result.Value);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ConvertValue_Generic_WhenConversionSucceeds_ReturnsValue()
    {
        var result = _sut.ConvertValue<int>("123");

        Assert.Equal(123, result);
    }

    [Fact]
    public void ConvertValue_Generic_WhenConversionFails_Throws()
    {
        Assert.Throws<KaleidoFrameworkException>(() =>
            _sut.ConvertValue<int>("not-a-number"));
    }

    public static TheoryData<Type, string, string?> ScalarDescriptorCases { get; } =
        new()
        {
            { typeof(string), "string", null },
            { typeof(bool), "boolean", null },
            { typeof(byte), "integer", null },
            { typeof(sbyte), "integer", null },
            { typeof(short), "integer", null },
            { typeof(ushort), "integer", null },
            { typeof(int), "integer", null },
            { typeof(uint), "integer", null },
            { typeof(long), "integer", "int64" },
            { typeof(ulong), "integer", "int64" },
            { typeof(float), "number", "float" },
            { typeof(double), "number", "double" },
            { typeof(decimal), "number", "decimal" },
            { typeof(Guid), "string", "uuid" },
            { typeof(DateOnly), "string", "date" },
            { typeof(TimeOnly), "string", "time" },
            { typeof(DateTime), "string", "date-time" },
            { typeof(DateTimeOffset), "string", "date-time-offset" },
            { typeof(TimeSpan), "string", "duration" }
        };

    [Theory]
    [MemberData(nameof(ScalarDescriptorCases))]
    public void GetDescriptor_WhenScalarType_MapsTypeAndFormat(
        Type type,
        string expectedType,
        string? expectedFormat)
    {
        var descriptor = _sut.GetDescriptor(
            typeof(ScalarModel<>).MakeGenericType(type).GetProperty("Value")!);

        Assert.Equal(expectedType, descriptor.Type);
        Assert.Equal(expectedFormat, descriptor.Format);
    }

    public static TheoryData<Type, bool> SupportedTypeCases { get; } =
        new()
        {
            { typeof(int), true },
            { typeof(int?), true },
            { typeof(string), true },
            { typeof(TestStatus), true },
            { typeof(TestStatus?), true },
            { typeof(Guid), true },
            { typeof(List<int>), false },
            { typeof(TestObject), false }
        };

    [Theory]
    [MemberData(nameof(SupportedTypeCases))]
    public void IsSupportedType_ReturnsExpected(Type type, bool expected)
    {
        Assert.Equal(expected, _sut.IsSupportedType(type));
    }

    [Fact]
    public void TryConvertValue_WhenValueIsJsonNull_ConvertsToNull()
    {
        using var document = JsonDocument.Parse("null");

        var result = _sut.TryConvertValue(document.RootElement, typeof(int?));

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryConvertValue_WhenJsonStringToStringType_PassesThrough()
    {
        using var document = JsonDocument.Parse("\"hello\"");

        var result = _sut.TryConvertValue(document.RootElement, typeof(string));

        Assert.True(result.Success);
        Assert.Equal("hello", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenValueAlreadyTargetType_ShortCircuits()
    {
        var expected = Guid.NewGuid();

        var result = _sut.TryConvertValue(expected, typeof(Guid));

        Assert.True(result.Success);
        Assert.Equal(expected, Assert.IsType<Guid>(result.Value));
    }

    [Fact]
    public void TryConvertValue_WhenEnumNumericNotDefined_ReturnsFailure()
    {
        var result = _sut.TryConvertValue("99", typeof(TestStatus));

        Assert.False(result.Success);
    }

    [Fact]
    public void ConvertValue_WhenConversionSucceeds_ReturnsValue()
    {
        var result = _sut.ConvertValue("42", typeof(int));

        Assert.Equal(42, Assert.IsType<int>(result));
    }

    private sealed class ScalarModel<T>
    {
        public T? Value { get; init; }
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

        public List<string> Names { get; init; } = [];
    }

    private sealed class TestObject
    {
    }
}
