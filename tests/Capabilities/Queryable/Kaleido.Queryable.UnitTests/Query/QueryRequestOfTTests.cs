using Kaleido.UnitTests;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestOfTTests
    : SutFixture
{
    private sealed record WardParameters(string Ward);

    private static QueryRequest<WardParameters> CreateSut(WardParameters? parameters, QueryBody? query = null) => new(parameters, query);

    // What a source or view reads through the untyped request.
    [Fact]
    public void AsAnUntypedRequest_ExposesTheParametersAndTheirDeclaredType()
    {
        var parameters = new WardParameters("B");
        IQueryRequest request = CreateSut(parameters);

        Assert.Same(parameters, request.ViewParameters);
        Assert.Equal(typeof(WardParameters), request.ViewParametersType);
    }

    [Fact]
    public void WithQuery_ExposesTypedAndUntypedParameters()
    {
        var parameters = new WardParameters("Alpha");
        IQueryRequest request = CreateSut(parameters, new QueryBody(SearchText: "alpha"));

        Assert.Same(parameters, ((QueryRequest<WardParameters>)request).ViewParameters);
        Assert.Same(parameters, request.ViewParameters);
        Assert.Equal(typeof(WardParameters), request.ViewParametersType);
        Assert.Equal("alpha", request.Query!.SearchText);
    }

    [Fact]
    public void WithoutParameters_StillDeclaresTheirType()
    {
        IQueryRequest request = CreateSut(null);

        Assert.Null(request.ViewParameters);
        Assert.Equal(typeof(WardParameters), request.ViewParametersType);
    }
}
