namespace Kaleido.Http.Queryable.UnitTests;

public sealed class QueryableContractUrlsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData(null, "/queryable")]
    [InlineData("", "/queryable")]
    [InlineData("   ", "/queryable")]
    [InlineData("members", "/members/queryable")]
    [InlineData(" /members/ ", "/members/queryable")]
    public void QueryablePrefix_NormalizesTheServiceName(string? serviceName, string expected)
    {
        Assert.Equal(expected, QueryableContractUrls.QueryablePrefix(serviceName!));
    }

    [Fact]
    public void QuerySourceQuery_AppendsTheSourceQueryRoute()
    {
        Assert.Equal("/members/queryable/Member/query", QueryableContractUrls.QuerySourceQuery("members", "Member"));
    }

    [Fact]
    public void QueryViewQuery_AppendsTheViewQueryRouteUnderItsSource()
    {
        Assert.Equal("/queryable/Member/Summary/query", QueryableContractUrls.QueryViewQuery("", "Member", "Summary"));
    }
}
