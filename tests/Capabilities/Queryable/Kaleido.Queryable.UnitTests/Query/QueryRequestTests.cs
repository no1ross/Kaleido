namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestTests : Kaleido.UnitTests.SutFixture<QueryRequest>
{
    protected override QueryRequest CreateSut() => new();

    [Fact]
    public void QueryRequest_UsesEmptyQueryViewParametersByDefault()
    {
        IQueryRequest request = CreateSut();

        Assert.IsType<EmptyQueryViewParameters>(request.ViewParameters);
        Assert.Equal(typeof(EmptyQueryViewParameters), request.ViewParametersType);
    }
}
