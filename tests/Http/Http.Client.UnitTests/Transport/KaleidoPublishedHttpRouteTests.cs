namespace Kaleido.Http.Client.Transport.UnitTests;

public sealed class KaleidoPublishedHttpRouteTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void RequirePath_AcceptsOnlyAPublishedRouteUnderItsVersion()
    {
        Assert.Equal("/clinic/1/queryable/patientsource/query",
            KaleidoPublishedHttpRoute.RequirePath("/clinic/1/queryable/patientsource/query", "/clinic/1"));
    }

    [Theory]
    [InlineData("https://other.test/clinic/1/query")]
    [InlineData("//other.test/clinic/1/query")]
    [InlineData("/clinic/1/../other/query")]
    [InlineData("/clinic/1/%2e%2e/other/query")]
    [InlineData("/clinic/1\\query")]
    [InlineData("/clinic/1/query?redirect=other")]
    [InlineData("/other/1/query")]
    [InlineData("/clinic/2/query")]
    public void RequirePath_RejectsAnyRouteThatCouldEscapeThePublishedVersion(string path)
    {
        var exception = Assert.Throws<KaleidoRegistryClientException>(() => KaleidoPublishedHttpRoute.RequirePath(path, "/clinic/1"));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
    }

    [Fact]
    public void ProcessState_ExpandsOnlyThePublishedTemplate()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"/clinic/1/processes/{id}",
            KaleidoPublishedHttpRoute.ProcessState("/clinic/1/processes/{processId}", id, "/clinic/1"));
        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable,
            Assert.Throws<KaleidoRegistryClientException>(() => KaleidoPublishedHttpRoute.ProcessState("/clinic/1/processes", id, "/clinic/1")).Code);
    }
}
