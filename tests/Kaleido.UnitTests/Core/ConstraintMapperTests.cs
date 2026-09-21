using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido.Abstractions.UnitTests;

public sealed class ConstraintMapperTests
{
    [Fact]
    public void Map_WhenPropertyIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                ConstraintMapper.Map(null!));

        Assert.Equal(
            "property",
            exception.ParamName);
    }

    [Fact]
    public void Map_WhenPropertyHasKnownValidationAttributes_MapsExpectedConstraints()
    {
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Name))!;

        var constraints = ConstraintMapper.Map(property);

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

        var constraint = Assert.Single(ConstraintMapper.Map(property));

        Assert.Equal("CustomRule", constraint.Type);
        Assert.Empty(constraint.Parameters);
    }

    private sealed class TestModel
    {
        [Required]
        [StringLength(10, MinimumLength = 3)]
        [RegularExpression("^[A-Z]+$")]
        public string Name { get; init; } = string.Empty;

        [CustomRule]
        public string Code { get; init; } = string.Empty;
    }

    private sealed class CustomRuleAttribute : ValidationAttribute
    {
    }
}
