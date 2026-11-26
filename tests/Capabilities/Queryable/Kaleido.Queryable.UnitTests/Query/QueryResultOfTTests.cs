using Kaleido.UnitTests;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryResultOfTTests : SutFixture
{
    private static QueryResult<TestRecord> CreateSut(TestRecord record) => new(2, 3, 4, [record]);

    [Fact]
    public void QueryResult_PreservesConstructorValues()
    {
        var record = new TestRecord("record");
        var result = CreateSut(record);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(3, result.Offset);
        Assert.Equal(4, result.PageSize);
        Assert.Same(record, result.Results.Single());
    }

    private sealed record TestRecord(string Name);
}
