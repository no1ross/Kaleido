using Kaleido.Http.Queryable;
using Kaleido.Json;

namespace Kaleido.Http.UnitTests.Queryable;

public sealed class QueryableValueNormalizerTests
{
    private static QueryableValueNormalizer CreateSut(IValueConverter? converter = null) =>
        new(converter ?? Mock.Of<IValueConverter>());

    [Fact]
    public void Normalize_WhenQueryIsNull_ReturnsNull()
    {
        var sut = CreateSut();
        var metadata = new QueryContextMetadata(
            Name: "test",
            Description: "desc",
            DisplayName: "Test",
            Version: "1",
            Source: null,
            Kind: QueryContextKind.Local,
            Pageable: null,
            Fields: []);

        var result = sut.Normalize(null, metadata);

        Assert.Null(result);
    }
}
