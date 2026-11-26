using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.Metadata.UnitTests;

public sealed class QuerySourceMetadataExtensionsTests : SutFixture
{
    private static QuerySourceMetadata Metadata(params FieldMetadata[] fields) =>
        new("Members", "Members.", "Members", null, QuerySourceKind.Local, null, fields, [], [], AuthorizationMetadata.Unspecified);

    [Fact]
    public void GetField_MatchesFieldNameWithoutCase()
    {
        var field = new FieldMetadata { Name = "MemberNumber", FieldType = typeof(string), DataType = new DataTypeDescriptor("string") };

        Assert.Same(field, Metadata(field).GetField("membernumber"));
    }

    [Fact]
    public void GetField_WhenFieldIsMissing_ReportsSourceAndField()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() => Metadata().GetField("Unknown"));

        Assert.Equal(QueryableErrorCodes.InvalidField, exception.Code);
        Assert.Contains("Unknown", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Members", exception.Message, StringComparison.Ordinal);
    }
}
