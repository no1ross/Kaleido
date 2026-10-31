using System.Text.Json;
using Kaleido.Exceptions;

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

    [Fact]
    public void ToQueryBody_WithoutSortOrPage_LeavesThemNull()
    {
        var result = new QueryApiBody { SearchText = "x" }.ToQueryBody(Fields);

        Assert.Null(result!.Sort);
        Assert.Null(result.Page);
    }

    [Fact]
    public void ToQueryBody_Sort_KeepsFieldAndSequence()
    {
        var body = new QueryApiBody
        {
            Sort = [new QueryApiSort { Field = "Name", Direction = "ASCENDING", Sequence = 2 }]
        };

        var sort = Assert.Single(body.ToQueryBody(Fields)!.Sort!);

        Assert.Equal("Name", sort.Field);
        Assert.Equal(SortDirection.Ascending, sort.Direction);
        Assert.Equal(2, sort.Sequence);
    }

    [Fact]
    public void ToQueryBody_WhenSortDirectionIsInvalid_ThrowsValidationException()
    {
        var body = new QueryApiBody
        {
            Sort = [new QueryApiSort { Field = "Id", Direction = "sideways" }]
        };

        var exception = Assert.Throws<KaleidoValidationException>(() => body.ToQueryBody(Fields));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal("The sort direction is not valid. Expected 'ascending' or 'descending'.", exception.Message);
    }

    [Fact]
    public void ToQueryBody_WhenLogicalOperatorIsInvalid_ThrowsValidationException()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Group = new QueryApiFilterGroup { Operator = "xor", Filters = [] }
            }
        };

        var exception = Assert.Throws<KaleidoValidationException>(() => body.ToQueryBody(Fields));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal("The logical operator of a filter group is not valid. Expected 'and' or 'or'.", exception.Message);
    }

    // #520: the refusal lists what is accepted, never the submitted text.
    [Fact]
    public void ToQueryBody_WhenFilterOperatorIsInvalid_ListsTheOperators_WithoutTheSubmittedText()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() =>
            ApiBody("Name", "near", StringValue("x")).ToQueryBody(Fields));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.StartsWith("The filter operator is not valid. Expected one of: equals, notEquals, ", exception.Message, StringComparison.Ordinal);
        Assert.Contains("startsWith", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("near", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToQueryBody_FilterNodeWithoutConditionOrGroup_KeepsTheEmptyNodeForValidation()
    {
        var body = new QueryApiBody { Filter = new QueryApiFilterNode() };

        var filter = body.ToQueryBody(Fields)!.Filter;

        Assert.NotNull(filter);
        Assert.Null(filter.Condition);
        Assert.Null(filter.Group);
    }

    [Fact]
    public void ToQueryBody_FilterNodeWithConditionAndGroup_KeepsBothForValidation()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Condition = ApiBody("Name", "equals", StringValue("a")).Filter!.Condition,
                Group = new QueryApiFilterGroup { Operator = "and", Filters = [ApiBody("Id", "equals", IntValue(1)).Filter!] }
            }
        };

        var filter = body.ToQueryBody(Fields)!.Filter!;

        Assert.Equal("Name", filter.Condition!.Field);
        Assert.Equal("Id", Assert.Single(filter.Group!.Filters).Condition!.Field);
    }

    [Fact]
    public void ToQueryBody_GroupWithoutFilters_ResolvesToAnEmptyGroup()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Group = new QueryApiFilterGroup { Operator = "or", Filters = null }
            }
        };

        var group = body.ToQueryBody(Fields)!.Filter!.Group!;

        Assert.Equal(LogicalOperator.Or, group.Operator);
        Assert.Empty(group.Filters);
    }

    [Fact]
    public void ToQueryBody_GroupKeepsEmptyNodesForValidation()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Group = new QueryApiFilterGroup
                {
                    Operator = "and",
                    Filters = [new QueryApiFilterNode(), ApiBody("Name", "equals", StringValue("a")).Filter!]
                }
            }
        };

        var filters = body.ToQueryBody(Fields)!.Filter!.Group!.Filters;

        Assert.Equal(2, filters.Count);
        Assert.Null(filters[0].Condition);
        Assert.Null(filters[0].Group);
        Assert.Equal("Name", filters[1].Condition!.Field);
    }

    [Fact]
    public void ToQueryBody_ConditionWithoutValues_ResolvesToNoValues()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Condition = new QueryApiFilterCondition { Field = "Name", Operator = "isnull", Values = null }
            }
        };

        Assert.Empty(body.ToQueryBody(Fields)!.Filter!.Condition!.Values);
    }

    [Fact]
    public void ToQueryBody_NullAndUndefinedValues_ResolveToNull()
    {
        var values = Resolve(typeof(int), JsonSerializer.SerializeToElement<object?>(null), default);

        Assert.Equal(new object?[] { null, null }, values);
    }

    [Fact]
    public void ToQueryBody_UnknownField_KeepsTheRawJson()
    {
        var body = ApiBody("Bogus", "equals", IntValue(7), StringValue("x"));

        Assert.Equal(["7", "\"x\""], body.ToQueryBody(Fields)!.Filter!.Condition!.Values);
    }

    [Fact]
    public void ToQueryBody_BooleanValues_ResolveToBooleans()
    {
        Assert.Equal(
            [true, false],
            Resolve(typeof(bool), JsonSerializer.SerializeToElement(true), JsonSerializer.SerializeToElement(false)));
    }

    public static TheoryData<Type> Int32FieldTypes { get; } =
        [typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(byte), typeof(sbyte), typeof(int?)];

    [Theory]
    [MemberData(nameof(Int32FieldTypes))]
    public void ToQueryBody_NumberForASmallIntegerField_ResolvesToInt32(Type fieldType)
    {
        Assert.Equal([5], Resolve(fieldType, IntValue(5)));
    }

    [Theory]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    public void ToQueryBody_NumberForALongField_ResolvesToInt64(Type fieldType)
    {
        Assert.Equal([5_000_000_000L], Resolve(fieldType, JsonSerializer.SerializeToElement(5_000_000_000L)));
    }

    [Fact]
    public void ToQueryBody_NumberForAFloatField_ResolvesToSingle()
    {
        Assert.Equal([1.5f], Resolve(typeof(float), JsonSerializer.SerializeToElement(1.5)));
    }

    [Fact]
    public void ToQueryBody_NumberForADoubleField_ResolvesToDouble()
    {
        Assert.Equal([1.25d], Resolve(typeof(double), JsonSerializer.SerializeToElement(1.25)));
    }

    [Fact]
    public void ToQueryBody_NumberForADecimalField_ResolvesToDecimal()
    {
        Assert.Equal([12.34m], Resolve(typeof(decimal), JsonSerializer.SerializeToElement(12.34m)));
    }

    [Fact]
    public void ToQueryBody_NumberForANonNumericField_KeepsTheRawText()
    {
        Assert.Equal(["42"], Resolve(typeof(string), IntValue(42)));
    }

    [Fact]
    public void ToQueryBody_UnsupportedJsonKind_ThrowsValidationException()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() =>
            Resolve(typeof(int), JsonDocument.Parse("[1]").RootElement));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal("A value for field 'field' is a JSON array; expected a value of type 'Int32'.", exception.Message);
    }

    [Fact]
    public void ToQueryBody_StringValues_ResolveToTheFieldsType()
    {
        var guid = Guid.Parse("6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13");

        Assert.Equal([guid], Resolve(typeof(Guid), StringValue(guid.ToString())));
        Assert.Equal([new DateOnly(2024, 2, 29)], Resolve(typeof(DateOnly), StringValue("2024-02-29")));
        Assert.Equal([new TimeOnly(13, 45)], Resolve(typeof(TimeOnly), StringValue("13:45")));
        Assert.Equal([new DateTime(2024, 2, 29, 13, 45, 0)], Resolve(typeof(DateTime), StringValue("2024-02-29T13:45:00")));
        Assert.Equal(
            [new DateTimeOffset(2024, 2, 29, 13, 45, 0, TimeSpan.FromHours(2))],
            Resolve(typeof(DateTimeOffset), StringValue("2024-02-29T13:45:00+02:00")));
        Assert.Equal([TimeSpan.FromMinutes(90)], Resolve(typeof(TimeSpan), StringValue("01:30:00")));
        Assert.Equal([TestStatus.Inactive], Resolve(typeof(TestStatus?), StringValue("INACTIVE")));
        Assert.Equal(["https://example"], Resolve(typeof(Uri), StringValue("https://example")));
    }

    [Theory]
    [InlineData(typeof(Guid), "a valid Guid")]
    [InlineData(typeof(DateOnly), "a valid date (yyyy-MM-dd)")]
    [InlineData(typeof(TimeOnly), "a valid time")]
    [InlineData(typeof(DateTime), "a valid date-time")]
    [InlineData(typeof(DateTimeOffset), "a valid date-time-offset")]
    [InlineData(typeof(TimeSpan), "a valid duration")]
    public void ToQueryBody_InvalidStringForTheFieldsType_ThrowsValidationException(Type fieldType, string expected)
    {
        var exception = Assert.Throws<KaleidoValidationException>(() => Resolve(fieldType, StringValue("nope")));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal($"A value for field 'field' is not {expected}.", exception.Message);
    }

    // #520: the field and the expected type are named, never the value (a name or identifier).
    [Fact]
    public void ToQueryBody_InvalidEnumString_NamesTheFieldAndTheEnum_NotTheValue()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() => Resolve(typeof(TestStatus), StringValue("nope")));

        Assert.Equal("A value for field 'field' is not a valid 'TestStatus' value.", exception.Message);
    }

    private static object?[] Resolve(Type fieldType, params JsonElement[] values)
    {
        IReadOnlyCollection<QueryableFieldDescriptor> fields =
        [
            new QueryableFieldDescriptor
            {
                Name = "Field",
                FieldType = fieldType,
                DataType = new DataTypeDescriptor("string"),
                IsFilterable = true,
                FilterOperators = [FilterOperator.Equals]
            }
        ];

        return ApiBody("field", "equals", values).ToQueryBody(fields)!.Filter!.Condition!.Values.ToArray();
    }

    // ---------------------------------------------------------------------------
    // Test types
    // ---------------------------------------------------------------------------

    private enum TestStatus { Unknown, Active, Inactive }
}
