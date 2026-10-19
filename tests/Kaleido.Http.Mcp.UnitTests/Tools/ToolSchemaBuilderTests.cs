namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolSchemaBuilderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolSchemaBuilder CreateSut() => new();

    private static ConstraintMetadata Constraint(string type, params (string Name, object? Value)[] parameters) =>
        new()
        {
            Type = type,
            Parameters = parameters.Select(x => new ConstraintParameterMetadata { Name = x.Name, Value = x.Value }).ToArray()
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
        var schema = CreateSut().ForValue(new DataTypeMetadata(type, format));

        Assert.Equal(expectedType, schema["type"]?.GetValue<string>());
        Assert.Equal(expectedFormat, schema["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_AnEnum_ListsCamelCaseNames_AndDescribesThem()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata(
                "string",
                EnumValues:
                [
                    new EnumValueMetadata(0, "Outpatient", "At a clinic"),
                    new EnumValueMetadata(1, "Inpatient", null)
                ]));

        Assert.Equal(["outpatient", "inpatient"], schema["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("outpatient: At a clinic", schema["description"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_AnArray_DescribesItsItems()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("array", ItemType: new DataTypeMetadata("string", "uuid")));

        Assert.Equal("array", schema["type"]?.GetValue<string>());
        Assert.Equal("uuid", schema["items"]?["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_CarriesTheDescription()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("string"), "  The member's id.  ");

        Assert.Equal("The member's id.", schema["description"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_MapsConstraints_IncludingValuesRoundTrippedAsJson()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("string"),
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
            new DataTypeMetadata("number"),
            constraints: [Constraint("Range", ("Minimum", 0.5m), ("Maximum", 10))]);

        Assert.Equal(0.5m, schema["minimum"]?.GetValue<decimal>());
        Assert.Equal(10, schema["maximum"]?.GetValue<long>());
    }

    [Fact]
    public void ForValue_RequiredText_RejectsEmptyText_AndFormatsMapEmailAndUrl()
    {
        var builder = CreateSut();

        Assert.Equal(1, builder.ForValue(new DataTypeMetadata("string"), constraints: [Constraint("Required")])["minLength"]?.GetValue<int>());
        Assert.Equal("email", builder.ForValue(new DataTypeMetadata("string"), constraints: [Constraint("EmailAddress")])["format"]?.GetValue<string>());
        Assert.Equal("uri", builder.ForValue(new DataTypeMetadata("string"), constraints: [Constraint("Url")])["format"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_LengthOnAnArray_LimitsItems()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("array", ItemType: new DataTypeMetadata("string")),
            constraints: [Constraint("MaxLength", ("Length", 3)), Constraint("MinLength", ("Length", 1))]);

        Assert.Equal(3, schema["maxItems"]?.GetValue<long>());
        Assert.Equal(1, schema["minItems"]?.GetValue<long>());
    }

    [Theory]
    [InlineData("time", "time")]
    [InlineData("date-time", "date-time")]
    [InlineData("byte", null)]
    [InlineData(null, null)]
    public void ForValue_AString_KeepsOnlyTheFormatsJsonSchemaKnows(string? format, string? expectedFormat)
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("string", format));

        Assert.Equal("string", schema["type"]?.GetValue<string>());
        Assert.Equal(expectedFormat, schema["format"]?.GetValue<string>());
        Assert.Null(schema["description"]);
    }

    [Fact]
    public void ForValue_ADuration_SaysHowToWriteIt()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("string", "duration"));

        Assert.Equal("An ISO 8601 duration, e.g. PT30M.", schema["description"]?.GetValue<string>());
    }

    [Fact]
    public void ForValue_AnArrayOfUnknownItems_AcceptsAnyItem()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("array"));

        Assert.Equal("array", schema["type"]?.GetValue<string>());
        Assert.Empty(Assert.IsType<JsonObject>(schema["items"]));
    }

    [Fact]
    public void ForValue_AnObject_KeepsItsType()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("object"));

        Assert.Equal(["type"], schema.Select(x => x.Key));
        Assert.Equal("object", schema["type"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ForValue_AnUntypedValue_AcceptsAnyValue(string type)
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata(type));

        Assert.Empty(schema);
    }

    [Fact]
    public void ForValue_AnEnumWithoutDescriptions_HasNoDescription()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("string", EnumValues: [new EnumValueMetadata(0, "Outpatient", " ")]));

        Assert.Equal(["outpatient"], schema["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Null(schema["description"]);
    }

    [Fact]
    public void ForValue_LengthOnAString_LimitsItsLength()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("string"),
            constraints: [Constraint("MaxLength", ("Length", 30)), Constraint("MinLength", ("Length", 2))]);

        Assert.Equal(30, schema["maxLength"]?.GetValue<long>());
        Assert.Equal(2, schema["minLength"]?.GetValue<long>());
        Assert.Null(schema["maxItems"]);
    }

    [Fact]
    public void ForValue_LengthOnAnUntypedValue_LimitsItsLength()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata(string.Empty),
            constraints: [Constraint("MaxLength", ("Length", 4)), Constraint("Required")]);

        Assert.Equal(4, schema["maxLength"]?.GetValue<long>());
        Assert.Null(schema["minLength"]);
    }

    [Fact]
    public void ForValue_AStringLengthWithoutAMinimum_SetsOnlyTheMaximum()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("string"),
            constraints: [Constraint("StringLength", ("MaximumLength", 10), ("MinimumLength", 0))]);

        Assert.Equal(10, schema["maxLength"]?.GetValue<long>());
        Assert.Null(schema["minLength"]);
    }

    [Fact]
    public void ForValue_RequiredText_KeepsALongerMinimumLength()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("string"),
            constraints: [Constraint("StringLength", ("MaximumLength", 10), ("MinimumLength", 3)), Constraint("Required")]);

        Assert.Equal(3, schema["minLength"]?.GetValue<long>());
    }

    [Fact]
    public void ForValue_ARequiredNumber_HasNoLengthLimit()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("integer"), constraints: [Constraint("Required")]);

        Assert.Equal(["type"], schema.Select(x => x.Key));
    }

    [Fact]
    public void ForValue_ConstraintParameters_AreReadFromEveryShape()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("number"),
            constraints:
            [
                Constraint("Range", ("Minimum", "1.25"), ("Maximum", JsonSerializer.SerializeToElement(99.5))),
                Constraint("StringLength", ("MaximumLength", new Uri("https://example.org/7"))),
                Constraint("MinLength", ("Length", JsonSerializer.SerializeToElement("12")))
            ]);

        Assert.Equal(1.25m, schema["minimum"]?.GetValue<decimal>());
        Assert.Equal(99.5m, schema["maximum"]?.GetValue<decimal>());
        Assert.Null(schema["maxLength"]);
        Assert.Equal(12, schema["minLength"]?.GetValue<long>());
    }

    [Fact]
    public void ForValue_MissingOrEmptyConstraintParameters_SetNothing()
    {
        var schema = CreateSut().ForValue(
            new DataTypeMetadata("string"),
            constraints:
            [
                Constraint("Range"),
                Constraint("StringLength", ("MaximumLength", null), ("MinimumLength", JsonSerializer.SerializeToElement<object?>(null))),
                Constraint("MaxLength", ("Length", "many")),
                Constraint("RegularExpression", ("Pattern", "  ")),
                Constraint("RegularExpression"),
                Constraint("Compare", ("OtherProperty", "Name"))
            ]);

        Assert.Equal(["type"], schema.Select(x => x.Key));
    }

    [Fact]
    public void ForValue_WithoutADataType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().ForValue(null!));
    }

    [Fact]
    public void ForValue_ABlankDescription_IsLeftOut()
    {
        var schema = CreateSut().ForValue(new DataTypeMetadata("boolean"), "   ");

        Assert.Null(schema["description"]);
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
