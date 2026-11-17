namespace Kaleido.Queryable.Http.Client.UnitTests;

public sealed class QueryablePublishedHttpRouteTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void RequirePath_UsesThePublishedPathBeneathTheSelectedGatewayRoute()
    {
        var path = "/member/1/queryable/my-context/query";

        Assert.Equal(path, QueryablePublishedHttpRoute.RequirePath(path, "/member/1"));
    }

    [Theory]
    [InlineData("/member/2/queryable/my-context/query")]
    [InlineData("https://other.test/member/1/queryable/my-context/query")]
    [InlineData("//member/1/queryable/my-context/query")]
    [InlineData("/member/1/../secret")]
    [InlineData("/member/1/%2e%2e/secret")]
    [InlineData("/member/1/queryable/my-context/query?next=other")]
    public void RequirePath_RefusesUnsafeOrWrongVersionPaths(string path)
    {
        var exception = Assert.Throws<KaleidoRegistryClientException>(() => QueryablePublishedHttpRoute.RequirePath(path, "/member/1"));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
    }

    [Fact]
    public void RequirePath_RefusesANullPath()
    {
        Assert.Throws<KaleidoRegistryClientException>(() => QueryablePublishedHttpRoute.RequirePath(null, "/member/1"));
    }

    [Fact]
    public void RequirePath_RefusesAnUnsafeGatewayRoute()
    {
        var exception = Assert.Throws<KaleidoRegistryClientException>(() =>
            QueryablePublishedHttpRoute.RequirePath("/member/1/queryable/my-context/query", "https://other.test/member/1"));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
    }
}
