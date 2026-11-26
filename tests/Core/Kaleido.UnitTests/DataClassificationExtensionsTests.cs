using Microsoft.Extensions.Compliance.Classification;

namespace Kaleido.UnitTests;

public sealed class DataClassificationExtensionsTests
    : SutFixture
{
    [Fact]
    public void GetDataClassificationNames_ForAClassifiedProperty_ReturnsTaxonomyAndValue()
    {
        var property = typeof(Member).GetProperty(nameof(Member.Name));

        Assert.Equal(["Test:PHI"], property.GetDataClassificationNames());
    }

    [Fact]
    public void GetDataClassificationNames_ForSeveralClassifications_ReturnsEachOnce()
    {
        var property = typeof(Member).GetProperty(nameof(Member.Notes));

        Assert.Equal(["Test:PHI", "Test:PII"], property.GetDataClassificationNames().Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(nameof(Member.Plan))]
    [InlineData(nameof(Member.MemberId))]
    public void GetDataClassificationNames_ForAnUnclassifiedOrNotClassifiedProperty_IsEmpty(string propertyName)
    {
        Assert.Empty(typeof(Member).GetProperty(propertyName).GetDataClassificationNames());
    }

    [Fact]
    public void GetDataClassifications_WithoutAMember_IsEmpty()
    {
        Assert.Empty(((System.Reflection.ICustomAttributeProvider?)null).GetDataClassifications());
    }

    private sealed class PhiAttribute() : DataClassificationAttribute(new("Test", "PHI"));

    private sealed class PiiAttribute() : DataClassificationAttribute(new("Test", "PII"));

    private sealed class Member
    {
        [Phi]
        public string Name { get; init; } = string.Empty;

        [Phi]
        [Pii]
        public string Notes { get; init; } = string.Empty;

        public string Plan { get; init; } = string.Empty;

        [NoDataClassification]
        public string MemberId { get; init; } = string.Empty;
    }
}
