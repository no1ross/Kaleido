using Kaleido.Json;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryBodyExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly IValueConverter _converter = Mock.Of<IValueConverter>();

    [Fact]
    public void Normalize_WhenQueryIsNull_ReturnsNull()
    {
        var metadata = new QueryContextMetadata(
            Name: "test",
            Description: "desc",
            DisplayName: "Test",
            Version: "1",
            Source: null,
            Kind: QueryContextKind.Local,
            Pageable: null,
            Fields: []);

        var result = ((QueryBody?)null).Normalize(_converter, metadata);

        Assert.Null(result);
    }
}
