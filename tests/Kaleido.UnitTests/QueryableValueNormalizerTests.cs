using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Xunit;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableValueNormalizerTests
{
    [Fact]
    public void Normalize_WhenValuesIsNull_ReturnsNull()
    {
        var result = QueryableValueNormalizer.Normalize(null, []);

        Assert.Null(result);
    }

    [Fact]
    public void Normalize_WhenParametersIsNull_ReturnsOriginalValues()
    {
        var values = new Dictionary<string, object?> { ["key"] = "value" };

        var result = QueryableValueNormalizer.Normalize(values, null);

        Assert.Same(values, result);
    }

    [Fact]
    public void Normalize_WhenParametersEmpty_ReturnsOriginalValues()
    {
        var values = new Dictionary<string, object?> { ["key"] = "value" };

        var result = QueryableValueNormalizer.Normalize(values, []);

        Assert.Same(values, result);
    }

    [Fact]
    public void Normalize_ConvertsStringToInt()
    {
        var values = new Dictionary<string, object?> { ["age"] = "25" };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), descriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.Equal(25, result["age"]);
    }

    [Fact]
    public void Normalize_ConvertsStringToBool()
    {
        var values = new Dictionary<string, object?> { ["active"] = "true" };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(bool));
        var parameters = new[]
        {
            new QueryParameterMetadata("active", typeof(bool), descriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.Equal(true, result["active"]);
    }

    [Fact]
    public void Normalize_WhenValueNotFoundInParameters_Skips()
    {
        var values = new Dictionary<string, object?> { ["extra"] = "value" };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), descriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.DoesNotContain("extra", result.Keys);
    }

    [Fact]
    public void Normalize_WhenConversionFails_Throws()
    {
        var values = new Dictionary<string, object?> { ["age"] = "not-a-number" };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), descriptor, [], null)
        };

        Assert.Throws<InvalidParameterValueException>(() =>
            QueryableValueNormalizer.Normalize(values, parameters));
    }

    [Fact]
    public void Normalize_WhenValueIsNull_KeepsNull()
    {
        var values = new Dictionary<string, object?> { ["age"] = null };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), descriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.Null(result["age"]);
    }

    [Fact]
    public void Normalize_UsesCaseInsensitiveKeyMatching()
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["AGE"] = "25" };
        var descriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), descriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.Equal(25, result["age"]);
    }

    [Fact]
    public void Normalize_ConvertsMultipleParameters()
    {
        var values = new Dictionary<string, object?>
        {
            ["age"] = "25",
            ["name"] = "test",
            ["active"] = "true"
        };
        var intDescriptor = DataTypeMapper.GetDescriptor(typeof(int));
        var stringDescriptor = DataTypeMapper.GetDescriptor(typeof(string));
        var boolDescriptor = DataTypeMapper.GetDescriptor(typeof(bool));
        var parameters = new[]
        {
            new QueryParameterMetadata("age", typeof(int), intDescriptor, [], null),
            new QueryParameterMetadata("name", typeof(string), stringDescriptor, [], null),
            new QueryParameterMetadata("active", typeof(bool), boolDescriptor, [], null)
        };

        var result = QueryableValueNormalizer.Normalize(values, parameters);

        Assert.NotNull(result);
        Assert.Equal(25, result["age"]);
        Assert.Equal("test", result["name"]);
        Assert.Equal(true, result["active"]);
    }
}
