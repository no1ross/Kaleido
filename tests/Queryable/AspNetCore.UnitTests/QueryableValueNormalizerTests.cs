using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using System.Text.Json;

namespace Kaleido.Queryable.UnitTests.AspNetCore;

public sealed class QueryableValueNormalizerTests
{
    [Fact]
    public void NormalizeValues_WhenValuesAreNull_ReturnsNull()
    {
        var result = QueryableValueNormalizer.Normalize(
            (IReadOnlyDictionary<string, object?>?)null,
            Array.Empty<QueryParameterMetadata>());

        Assert.Null(result);
    }

    [Fact]
    public void NormalizeValues_WhenParametersAreEmpty_ReturnsOriginalDictionary()
    {
        var values = new Dictionary<string, object?> { ["Category"] = "A" };

        var result = QueryableValueNormalizer.Normalize(
            values,
            Array.Empty<QueryParameterMetadata>());

        Assert.Same(values, result);
    }

    [Fact]
    public void NormalizeValues_ConvertsKnownParameterTypes()
    {
        var values = new Dictionary<string, object?> { ["Amount"] = "12.5" };
        var parameters = new[] { new QueryParameterMetadata("Amount", typeof(decimal), [], null) };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.Equal(12.5m, Assert.IsType<decimal>(result!["Amount"]));
    }

    [Fact]
    public void NormalizeValues_WhenConversionFails_ThrowsInvalidParameterValueException()
    {
        var values = new Dictionary<string, object?> { ["Amount"] = "nope" };
        var parameters = new[] { new QueryParameterMetadata("Amount", typeof(decimal), [], null) };

        Assert.Throws<InvalidParameterValueException>(() => QueryableValueNormalizer.Normalize(values, parameters));
    }

    [Fact]
    public void NormalizeQuery_ConvertsFilterValuesToFieldType()
    {
        var query = new QueryBody(
            Filter: QueryFilterNode.CreateCondition("Amount", FilterOperator.Equals, JsonDocument.Parse("12.5").RootElement));

        var result = QueryableValueNormalizer.Normalize(query, CreateMetadata());
        var condition = Assert.IsType<QueryFilterCondition>(result!.Filter!.Condition);

        Assert.Equal(12.5m, Assert.IsType<decimal>(condition.Values.Single()));
    }

    [Fact]
    public void NormalizeQuery_WhenFieldIsMissing_ThrowsInvalidFieldException()
    {
        var query = new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A"));

        Assert.Throws<InvalidFieldException>(() => QueryableValueNormalizer.Normalize(query, CreateMetadata()));
    }

    [Fact]
    public void NormalizeQuery_WhenValueConversionFails_ThrowsInvalidFilterValueException()
    {
        var query = new QueryBody(Filter: QueryFilterNode.CreateCondition("Amount", FilterOperator.Equals, "nope"));

        Assert.Throws<InvalidFilterValueException>(() => QueryableValueNormalizer.Normalize(query, CreateMetadata()));
    }

    private static QueryContextMetadata CreateMetadata() =>
        new(
            "test-context",
            "Test Context",
            "Test Context",
            "1.0.0",
            "Unit Test",
            QueryContextKind.Direct,
            null,
            [new FieldMetadata("Amount", null, typeof(decimal), true, [FilterOperator.Equals], false, null, null, false)]);
}
