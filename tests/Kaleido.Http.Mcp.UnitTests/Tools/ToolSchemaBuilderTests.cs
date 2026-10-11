namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolSchemaBuilderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolSchemaBuilder CreateSut() => new();

    private static ConstraintContract Constraint(string type, params (string Name, object? Value)[] parameters) =>
        new()
        {
            Type = type,
            Parameters = parameters.Select(x => new ConstraintParameterContract { Name = x.Name, Value = x.Value }).ToArray()
        };

    [Theory]
    [InlineData("integer", null, "integer", null)]
    [InlineData("number", "decimal", "number", null)]
    [InlineData("boolean", null, "boolean", null)]
    [InlineData("string", "uuid", "string", "uuid")]
    [InlineData("string", "date", "string", "date")]
    [InlineData("string", "date-time-offset", "string", "date-time")]
    [InlineData("string", "duration", "string", "duration")]
    public void ForValue_MapsTypesAndFormats(string type, string? format, string expectedType, string? expectedFormat)
    {
        var schema = CreateSut().ForValue(new DataTypeDescriptor(type, format));

        Assert.Equal(expectedType, schema["type"]?.GetValue<string>());
        Assert.Equal(expectedFormat, schema["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_AnEnum_ListsCamelCaseNames_AndDescribesThem()
    {
        var schema = CreateSut().ForValue(
            new DataTypeDescriptor(
                "string",
                EnumValues:
                [
                    new EnumValueDescriptor(0, "Outpatient", "At a clinic"),
                    new EnumValueDescriptor(1, "Inpatient", null)
                ]));

        Assert.Equal(["outpatient", "inpatient"], schema["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("outpatient: At a clinic", schema["description"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_AnArray_DescribesItsItems()
    {
        var schema = CreateSut().ForValue(new DataTypeDescriptor("array", ItemType: new DataTypeDescriptor("string", "uuid")));

        Assert.Equal("array", schema["type"]?.GetValue<string>());
        Assert.Equal("uuid", schema["items"]?["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_CarriesTheDescription()
    {
        var schema = CreateSut().ForValue(new DataTypeDescriptor("string"), "  The member's id.  ");

        Assert.Equal("The member's id.", schema["description"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_MapsConstraints_IncludingValuesRoundTrippedAsJson()
    {
        var schema = CreateSut().ForValue(
            new DataTypeDescriptor("string"),
            constraints:
            [
                Constraint("StringLength", ("MaximumLength", JsonSerializer.SerializeToElement(20)), ("MinimumLength", 2)),
                Constraint("RegularExpression", ("Pattern", JsonSerializer.SerializeToElement("^[A-Z]+$"))),
                Constraint("Required")
            ]);

        Assert.Equal(20, schema["maxLength"]?.GetValue<long>());
        Assert.Equal(2, schema["minLength"]?.GetValue<long>());
        Assert.Equal("^[A-Z]+$", schema["pattern"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_Range_SetsMinimumAndMaximum()
    {
        var schema = CreateSut().ForValue(
            new DataTypeDescriptor("number"),
            constraints: [Constraint("Range", ("Minimum", 0.5m), ("Maximum", 10))]);

        Assert.Equal(0.5m, schema["minimum"]?.GetValue<decimal>());
        Assert.Equal(10, schema["maximum"]?.GetValue<long>());
    }

    [Fact]
    public void ForValue_RequiredText_RejectsEmptyText_AndFormatsMapEmailAndUrl()
    {
        var builder = CreateSut();

        Assert.Equal(1, builder.ForValue(new DataTypeDescriptor("string"), constraints: [Constraint("Required")])["minLength"]?.GetValue<int>());
        Assert.Equal("email", builder.ForValue(new DataTypeDescriptor("string"), constraints: [Constraint("EmailAddress")])["format"]?.GetValue<string>());
        Assert.Equal("uri", builder.ForValue(new DataTypeDescriptor("string"), constraints: [Constraint("Url")])["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_LengthOnAnArray_LimitsItems()
    {
        var schema = CreateSut().ForValue(
            new DataTypeDescriptor("array", ItemType: new DataTypeDescriptor("string")),
            constraints: [Constraint("MaxLength", ("Length", 3)), Constraint("MinLength", ("Length", 1))]);

        Assert.Equal(3, schema["maxItems"]?.GetValue<long>());
        Assert.Equal(1, schema["minItems"]?.GetValue<long>());
    }

    [Fact]
    public void IsRequired_IsTrueOnlyForARequiredConstraint()
    {
        var builder = CreateSut();

        Assert.True(builder.IsRequired([Constraint("Required")]));
        Assert.False(builder.IsRequired([Constraint("StringLength")]));
        Assert.False(builder.IsRequired(null));
    }
}
