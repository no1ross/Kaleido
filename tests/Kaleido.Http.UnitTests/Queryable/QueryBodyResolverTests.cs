using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Http.Queryable.UnitTests;

public sealed class QueryBodyResolverTests
    : Kaleido.UnitTests.SutFixture
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly IReadOnlyCollection<QueryableFieldDescriptor> Fields =
    [
        new QueryableFieldDescriptor
        {
            Name = "Name",
            FieldType = typeof(string),
            DataType = new DataTypeDescriptor("string"),
            IsFilterable = true,
            FilterOperators = [FilterOperator.Equals]
        },
        new QueryableFieldDescriptor
        {
            Name = "Id",
            FieldType = typeof(int),
            DataType = new DataTypeDescriptor("integer"),
            IsFilterable = true,
            FilterOperators = [FilterOperator.Equals]
        },
        new QueryableFieldDescriptor
        {
            Name = "Status",
            FieldType = typeof(TestStatus),
            DataType = new DataTypeDescriptor("string"),
            IsFilterable = true,
            FilterOperators = [FilterOperator.Equals]
        },
        new QueryableFieldDescriptor
        {
            Name = "Amount",
            FieldType = typeof(decimal),
            DataType = new DataTypeDescriptor("decimal"),
            IsFilterable = true,
            FilterOperators = [FilterOperator.Equals]
        }
    ];

    private static QueryApiBody ApiBody(
        string field,
        string @operator,
        params JsonElement[] values) =>
        new()
        {
            Filter = new QueryApiFilterNode
            {
                Condition = new QueryApiFilterCondition
                {
                    Field = field,
                    Operator = @operator,
                    Values = values
                }
            }
        };

    private static JsonElement StringValue(string value) =>
        JsonSerializer.SerializeToElement(value);

    private static JsonElement IntValue(int value) =>
        JsonSerializer.SerializeToElement(value);

    // ---------------------------------------------------------------------------
    // Null / empty body
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToQueryBody_WhenBodyIsNull_ReturnsNull()
    {
        var result = ((QueryApiBody?)null).ToQueryBody(Fields);
        Assert.Null(result);
    }

    [Fact]
    public void ToQueryBody_WhenBodyHasNoFilter_ReturnsBodyWithNullFilter()
    {
        var body = new QueryApiBody { SearchText = "hello" };

        var result = body.ToQueryBody(Fields);

        Assert.NotNull(result);
        Assert.Equal("hello", result.SearchText);
        Assert.Null(result.Filter);
    }

    // ---------------------------------------------------------------------------
    // Enum operator resolution
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToQueryBody_ParsesFilterOperatorCaseInsensitive()
    {
        var body = ApiBody("Name", "EQUALS", StringValue("alpha"));

        var result = body.ToQueryBody(Fields);

        Assert.Equal(FilterOperator.Equals, result!.Filter!.Condition!.Operator);
    }

    [Fact]
    public void ToQueryBody_WhenOperatorIsInvalid_ThrowsValidationException()
    {
        var body = ApiBody("Name", "notarealop", StringValue("x"));

        Assert.Throws<KaleidoValidationException>(() => body.ToQueryBody(Fields));
    }

    // ---------------------------------------------------------------------------
    // JsonElement value resolution
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToQueryBody_StringValue_ResolvesToString()
    {
        var body = ApiBody("Name", "equals", StringValue("Alpha"));

        var result = body.ToQueryBody(Fields);

        Assert.Equal("Alpha", result!.Filter!.Condition!.Values[0]);
    }

    [Fact]
    public void ToQueryBody_IntValue_ResolvesToInt32()
    {
        var body = ApiBody("Id", "equals", IntValue(42));

        var result = body.ToQueryBody(Fields);

        Assert.Equal(42, result!.Filter!.Condition!.Values[0]);
    }

    [Fact]
    public void ToQueryBody_EnumStringValue_ResolvesToEnum()
    {
        var body = ApiBody("Status", "equals", StringValue("Active"));

        var result = body.ToQueryBody(Fields);

        Assert.Equal(TestStatus.Active, result!.Filter!.Condition!.Values[0]);
    }

    [Fact]
    public void ToQueryBody_EnumStringValue_IsCaseInsensitive()
    {
        var body = ApiBody("Status", "equals", StringValue("active"));

        var result = body.ToQueryBody(Fields);

        Assert.Equal(TestStatus.Active, result!.Filter!.Condition!.Values[0]);
    }

    [Fact]
    public void ToQueryBody_InvalidEnumString_ThrowsValidationException()
    {
        var body = ApiBody("Status", "equals", StringValue("notastatus"));

        Assert.Throws<KaleidoValidationException>(() => body.ToQueryBody(Fields));
    }

    [Fact]
    public void ToQueryBody_UnknownField_PassesThroughUnchanged()
    {
        var body = ApiBody("Bogus", "equals", StringValue("x"));

        var result = body.ToQueryBody(Fields);

        // Unknown fields pass through — QueryRequestValidator handles the error downstream
        Assert.NotNull(result!.Filter!.Condition);
        Assert.Equal("Bogus", result.Filter.Condition.Field);
    }

    // ---------------------------------------------------------------------------
    // Nested groups
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToQueryBody_Group_ResolvesNestedConditions()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Group = new QueryApiFilterGroup
                {
                    Operator = "and",
                    Filters =
                    [
                        new QueryApiFilterNode
                        {
                            Condition = new QueryApiFilterCondition
                            {
                                Field = "Name",
                                Operator = "equals",
                                Values = [StringValue("Alpha")]
                            }
                        },
                        new QueryApiFilterNode
                        {
                            Condition = new QueryApiFilterCondition
                            {
                                Field = "Id",
                                Operator = "equals",
                                Values = [IntValue(1)]
                            }
                        }
                    ]
                }
            }
        };

        var result = body.ToQueryBody(Fields);

        Assert.NotNull(result!.Filter!.Group);
        Assert.Equal(LogicalOperator.And, result.Filter.Group.Operator);
        Assert.Equal(2, result.Filter.Group.Filters.Count);
        Assert.Equal("Alpha", result.Filter.Group.Filters[0].Condition!.Values[0]);
        Assert.Equal(1, result.Filter.Group.Filters[1].Condition!.Values[0]);
    }

    // ---------------------------------------------------------------------------
    // Sort / Page
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToQueryBody_Sort_ParsesDirection()
    {
        var body = new QueryApiBody
        {
            Sort = [new QueryApiSort { Field = "Id", Direction = "descending" }]
        };

        var result = body.ToQueryBody(Fields);

        Assert.Equal(SortDirection.Descending, result!.Sort![0].Direction);
    }

    [Fact]
    public void ToQueryBody_Page_MapsSizeAndOffset()
    {
        var body = new QueryApiBody
        {
            Page = new QueryApiPage { Size = 10, Offset = 20 }
        };

        var result = body.ToQueryBody(Fields);

        Assert.Equal(10, result!.Page!.Size);
        Assert.Equal(20, result.Page.Offset);
    }

    // ---------------------------------------------------------------------------
    // Test types
    // ---------------------------------------------------------------------------

    private enum TestStatus { Unknown, Active, Inactive }
}
