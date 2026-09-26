using System.ComponentModel.DataAnnotations;

namespace Kaleido.Abstractions.UnitTests;

public sealed class ConstraintMapperTests
{
    private readonly ConstraintMapper _sut = new();

    [Fact]
    public void Map_WhenPropertyIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                _sut.Map(null!));

        Assert.Equal(
            "propertyInfo",
            exception.ParamName);
    }

    [Fact]
    public void Map_WhenPropertyHasKnownValidationAttributes_MapsExpectedConstraints()
    {
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Name))!;

        var constraints = _sut.Map(property);

        Assert.Collection(
            constraints,
            required =>
            {
                Assert.Equal("Required", required.Type);
                Assert.Empty(required.Parameters);
            },
            stringLength =>
            {
                Assert.Equal("StringLength", stringLength.Type);
                Assert.Collection(
                    stringLength.Parameters,
                    maximum =>
                    {
                        Assert.Equal("MaximumLength", maximum.Name);
                        Assert.Equal(10, Assert.IsType<int>(maximum.Value));
                    },
                    minimum =>
                    {
                        Assert.Equal("MinimumLength", minimum.Name);
                        Assert.Equal(3, Assert.IsType<int>(minimum.Value));
                    });
            },
            regex =>
            {
                Assert.Equal("RegularExpression", regex.Type);
                var parameter = Assert.Single(regex.Parameters);
                Assert.Equal("Pattern", parameter.Name);
                Assert.Equal("^[A-Z]+$", Assert.IsType<string>(parameter.Value));
            });
    }

    [Fact]
    public void Map_WhenPropertyHasUnknownValidationAttribute_UsesAttributeNameWithoutSuffix()
    {
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Code))!;

        var constraint = Assert.Single(_sut.Map(property));

        Assert.Equal("CustomRule", constraint.Type);
        Assert.Empty(constraint.Parameters);
    }

    [Fact]
    public void Map_WhenPropertyHasRangeAttribute_MapsMinMaxParameters()
    {
        var constraint = Assert.Single(_sut.Map(Property(nameof(TestModel.Age))));

        Assert.Equal("Range", constraint.Type);
        Assert.Collection(
            constraint.Parameters,
            p => { Assert.Equal("Minimum", p.Name); Assert.Equal(0, Assert.IsType<int>(p.Value)); },
            p => { Assert.Equal("Maximum", p.Name); Assert.Equal(120, Assert.IsType<int>(p.Value)); });
    }

    [Fact]
    public void Map_WhenPropertyHasMaxAndMinLength_MapsLengthParameters()
    {
        var max = Assert.Single(_sut.Map(Property(nameof(TestModel.Max))));
        Assert.Equal("MaxLength", max.Type);
        Assert.Equal(5, Assert.IsType<int>(Assert.Single(max.Parameters).Value));

        var min = Assert.Single(_sut.Map(Property(nameof(TestModel.Min))));
        Assert.Equal("MinLength", min.Type);
        Assert.Equal(2, Assert.IsType<int>(Assert.Single(min.Parameters).Value));
    }

    [Fact]
    public void Map_WhenPropertyHasFormatAttributes_MapsConstraintTypesWithoutParameters()
    {
        Assert.Equal("EmailAddress", Assert.Single(_sut.Map(Property(nameof(TestModel.Email)))).Type);
        Assert.Equal("Phone", Assert.Single(_sut.Map(Property(nameof(TestModel.Phone)))).Type);
        Assert.Equal("Url", Assert.Single(_sut.Map(Property(nameof(TestModel.Url)))).Type);
    }

    private static System.Reflection.PropertyInfo Property(string name) =>
        typeof(TestModel).GetProperty(name)!;

    private sealed class TestModel
    {
        [Required]
        [StringLength(10, MinimumLength = 3)]
        [RegularExpression("^[A-Z]+$")]
        public string Name { get; init; } = string.Empty;

        [CustomRule]
        public string Code { get; init; } = string.Empty;

        [Range(0, 120)]
        public int Age { get; init; }

        [MaxLength(5)]
        public string Max { get; init; } = string.Empty;

        [MinLength(2)]
        public string Min { get; init; } = string.Empty;

        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Phone]
        public string Phone { get; init; } = string.Empty;

        [Url]
        public string Url { get; init; } = string.Empty;
    }

    private sealed class CustomRuleAttribute : ValidationAttribute
    {
    }
}
