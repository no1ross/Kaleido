namespace Kaleido.Http.Queryable.UnitTests;

public sealed class QueryableContractUrlsTests : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData("members", "1", "/members/1/queryable")]
    [InlineData(" /members/ ", " /2026-11/ ", "/members/2026-11/queryable")]
    public void QueryablePrefix_AlwaysIncludesTheServiceVersion(string service, string version, string expected)
    {
        Assert.Equal(expected, QueryableContractUrls.QueryablePrefix(service, version));
    }

    [Fact]
    public void VersionedRoutes_PinEachQueryToItsServiceVersion()
    {
        Assert.Equal("/members/2026-11/queryable/Member/query", QueryableContractUrls.QuerySourceQuery("members", "2026-11", "Member"));
        Assert.Equal("/members/2026-11/queryable/Member/Summary/query", QueryableContractUrls.QueryViewQuery("members", "2026-11", "Member", "Summary"));
    }

    [Fact]
    public void VersionedRoutes_RequireAServiceAndVersion()
    {
        Assert.Throws<ArgumentException>(() => QueryableContractUrls.QueryablePrefix("", "1"));
        Assert.Throws<ArgumentException>(() => QueryableContractUrls.QueryablePrefix("members", ""));
    }
}
