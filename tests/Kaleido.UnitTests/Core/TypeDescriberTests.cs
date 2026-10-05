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
