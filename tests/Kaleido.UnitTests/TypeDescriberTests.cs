using System.ComponentModel;

namespace Kaleido.UnitTests;

public sealed class TypeDescriberTests
    : Kaleido.UnitTests.SutFixture
{
    private static TypeDescriber CreateSut() =>
        new();

    private readonly TypeDescriber _sut = CreateSut();

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
    public void GetDescriptor_ANonGenericCollection_IsAnArrayOfObjects()
    {
        var descriptor = _sut.GetDescriptor(typeof(System.Collections.ArrayList));

        Assert.Equal("array", descriptor.Type);
        Assert.Equal("object", descriptor.ItemType!.Type);
    }

    [Fact]
    public void GetDescriptor_ATypeArray_IsAnArrayOfItsElementType()
    {
        var descriptor = _sut.GetDescriptor(typeof(int[]));

        Assert.Equal("array", descriptor.Type);
        Assert.NotEqual("object", descriptor.ItemType!.Type);
        Assert.False(descriptor.Nullable);
    }

    [Fact]
    public void GetDescriptor_WhenEnumWithDescription_MapsEnumValues()
    {
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Status))!);

        Assert.Equal("string", descriptor.Type);
        Assert.Equal("enum", descriptor.Format);
        Assert.NotNull(descriptor.EnumValues);
        Assert.Equal(2, descriptor.EnumValues.Count);
    }

    [Fact]
    public void GetDescriptor_WhenArrayType_MapsWithItemType()
    {
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Ids))!);

        Assert.Equal("array", descriptor.Type);
        Assert.NotNull(descriptor.ItemType);
        Assert.Equal("string", descriptor.ItemType!.Type);
    }

    [Fact]
    public void GetDescriptor_WhenGenericList_MapsWithItemType()
    {
        var descriptor = _sut.GetDescriptor(typeof(TestModel).GetProperty(nameof(TestModel.Names))!);

        Assert.Equal("array", descriptor.Type);
        Assert.NotNull(descriptor.ItemType);
        Assert.Equal("string", descriptor.ItemType!.Type);
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

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(TestStatus), true)]
    [InlineData(typeof(TestStatus?), true)]
    [InlineData(typeof(TestObject), false)]
    [InlineData(typeof(List<int>), false)]
    public void IsSupportedType_AcceptsScalarsAndEnums(Type type, bool expected)
    {
        Assert.Equal(expected, _sut.IsSupportedType(type));
    }

    [Fact]
    public void IsSupportedType_WhenTypeIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _sut.IsSupportedType(null!));

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void GetDescriptor_WhenPropertyIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _sut.GetDescriptor((System.Reflection.PropertyInfo)null!));

        Assert.Equal("propertyInfo", exception.ParamName);
    }

    [Fact]
    public void GetDescriptor_WhenTypeIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _sut.GetDescriptor((Type)null!));

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void GetDescriptor_WhenRealArray_MapsWithItemType()
    {
        var descriptor = _sut.GetDescriptor(typeof(int?[]));

        Assert.Equal("array", descriptor.Type);
        Assert.False(descriptor.Nullable);
        Assert.Equal("integer", descriptor.ItemType!.Type);
        Assert.True(descriptor.ItemType.Nullable);
    }

    [Fact]
    public void GetDescriptor_WhenNonGenericEnumerable_MapsObjectItems()
    {
        var descriptor = _sut.GetDescriptor(typeof(System.Collections.ArrayList));

        Assert.Equal("array", descriptor.Type);
        Assert.Equal("object", descriptor.ItemType!.Type);
    }

    [Fact]
    public void GetDescriptor_WhenComplexType_MapsObjectAndCachesIt()
    {
        var first = _sut.GetDescriptor(typeof(TestObject));
        var second = _sut.GetDescriptor(typeof(TestObject));

        Assert.Equal("object", first.Type);
        Assert.Null(first.Format);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetDescriptor_WhenNullableEnumType_MarksNullableAndKeepsValues()
    {
        var descriptor = _sut.GetDescriptor(typeof(TestStatus?));

        Assert.True(descriptor.Nullable);
        Assert.Equal("enum", descriptor.Format);
        Assert.Equal(2, descriptor.EnumValues!.Count);
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
