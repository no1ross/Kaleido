using System.ComponentModel;
using Kaleido.Metadata;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class RecordDescriptorFactoryTests
{
    private readonly RecordDescriptorFactory _sut;

    public RecordDescriptorFactoryTests()
    {
        _sut = new RecordDescriptorFactory();
    }

    [Fact]
    public void Create_Should_Map_Record_Metadata()
    {
        var metadata = TestData.Metadata();

        var result = _sut.Create(metadata);

        Assert.Equal(metadata.Name, result.Name);
        Assert.Equal(metadata.Version, result.Version);
        Assert.Equal(metadata.Source, result.Source);
    }

    [Fact]
    public void Create_Should_Map_Pageable_Metadata()
    {
        var metadata = TestData.Metadata(
            pageable: new RuntimePageableMetadata(25, 500));

        var result = _sut.Create(metadata);

        Assert.NotNull(result.Pageable);

        Assert.Equal(25, result.Pageable!.DefaultSize);
        Assert.Equal(500, result.Pageable.MaxSize);
    }

    [Fact]
    public void Create_Should_Return_Null_Pageable_When_Not_Configured()
    {
        var result = _sut.Create(TestData.Metadata());

        Assert.Null(result.Pageable);
    }

    [Fact]
    public void Create_Should_Map_Allowed_Queries()
    {
        var metadata = TestData.Metadata(
            allowedQueries:
            [
                new RuntimeAllowedQueryMetadata(
                    "active",
                    "Active records",
                    ["status"])
            ]);

        var result = _sut.Create(metadata);

        var query = Assert.Single(result.AllowedQueries);

        Assert.Equal("active", query.Name);
        Assert.Equal("Active records", query.Description);
        Assert.Equal(["status"], query.Parameters);
    }

    [Theory]
    [InlineData(typeof(string), "string", null)]
    [InlineData(typeof(bool), "boolean", null)]

    [InlineData(typeof(byte), "integer", null)]
    [InlineData(typeof(sbyte), "integer", null)]
    [InlineData(typeof(short), "integer", null)]
    [InlineData(typeof(ushort), "integer", null)]
    [InlineData(typeof(int), "integer", null)]
    [InlineData(typeof(uint), "integer", null)]

    [InlineData(typeof(long), "integer", "int64")]
    [InlineData(typeof(ulong), "integer", "int64")]

    [InlineData(typeof(float), "number", "float")]
    [InlineData(typeof(double), "number", "double")]
    [InlineData(typeof(decimal), "number", "decimal")]

    [InlineData(typeof(Guid), "string", "uuid")]

    [InlineData(typeof(DateOnly), "string", "date")]
    [InlineData(typeof(TimeOnly), "string", "time")]

    [InlineData(typeof(DateTime), "string", "date-time")]
    [InlineData(typeof(DateTimeOffset), "string", "date-time-offset")]

    [InlineData(typeof(TimeSpan), "string", "duration")]
    public void Create_Should_Map_CLR_Types(
        Type fieldType,
        string expectedType,
        string? expectedFormat)
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Value",
                        fieldType)
                ]));

        var field = Assert.Single(result.Fields);

        Assert.Equal(expectedType, field.DataType.Type);
        Assert.Equal(expectedFormat, field.DataType.Format);
    }

    [Theory]
    [InlineData(typeof(int?), "integer")]
    [InlineData(typeof(bool?), "boolean")]
    [InlineData(typeof(Guid?), "string")]
    [InlineData(typeof(DateOnly?), "string")]
    [InlineData(typeof(DateTime?), "string")]
    public void Create_Should_Map_Nullable_Types(
        Type fieldType,
        string expectedType)
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Value",
                        fieldType)
                ]));

        var field = Assert.Single(result.Fields);

        Assert.Equal(expectedType, field.DataType.Type);
    }

    [Fact]
    public void Create_Should_Map_Enum_Field()
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Status",
                        typeof(TestStatus))
                ]));

        var field = Assert.Single(result.Fields);

        Assert.Equal("string", field.DataType.Type);
        Assert.Equal("enum", field.DataType.Format);
    }

    [Fact]
    public void Create_Should_Map_Unknown_Type_As_Object()
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Value",
                        typeof(TestComplexType))
                ]));

        var field = Assert.Single(result.Fields);

        Assert.Equal("object", field.DataType.Type);
    }

    [Fact]
    public void Create_Should_Map_Filter_Operator_Descriptions()
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Name",
                        typeof(string),
                        filterOperators:
                        [
                            FilterOperator.Contains,
                            FilterOperator.StartsWith
                        ])
                ]));

        var field = Assert.Single(result.Fields);

        Assert.NotEmpty(field.FilterOperators);
    }

    [Fact]
    public void Create_Should_Map_Match_Mode_Descriptions()
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Name",
                        typeof(string),
                        matchModes:
                        [
                            MatchMode.Exact,
                            MatchMode.Contains
                        ])
                ]));

        var field = Assert.Single(result.Fields);

        Assert.NotEmpty(field.MatchModes);
    }

    [Fact]
    public void Create_Should_Map_Filter_Search_And_Sort_Flags()
    {
        var result = _sut.Create(
            TestData.Metadata(
                fields:
                [
                    TestData.Field(
                        "Name",
                        typeof(string),
                        isFilterable: true,
                        isSearchable: true,
                        isSortable: true)
                ]));

        var field = Assert.Single(result.Fields);

        Assert.True(field.IsFilterable);
        Assert.True(field.IsSearchable);
        Assert.True(field.IsSortable);
    }

    private static class TestData
    {
        public static RuntimeRecordMetadata Metadata(
            IReadOnlyList<RuntimeFieldMetadata>? fields = null,
            IReadOnlyList<RuntimeAllowedQueryMetadata>? allowedQueries = null,
            RuntimePageableMetadata? pageable = null)
        {
            return new RuntimeRecordMetadata(
                "test-record",
                "1.0.0",
                "Unit Test",
                fields ?? [],
                allowedQueries ?? [],
                pageable);
        }

        public static RuntimeFieldMetadata Field(
            string name,
            Type type,
            bool isFilterable = false,
            bool isSearchable = false,
            bool isSortable = false,
            IReadOnlyList<FilterOperator>? filterOperators = null,
            IReadOnlyList<MatchMode>? matchModes = null)
        {
            return new RuntimeFieldMetadata(
                name,
                type,
                isFilterable,
                filterOperators ?? [],
                isSearchable,
                isSearchable ? 1 : null,
                matchModes ?? [],
                isSortable);
        }
    }

    private sealed class TestComplexType
    {
    }

    private enum TestStatus
    {
        [Description("Active")]
        Active
    }
}