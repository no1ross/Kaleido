namespace Kaleido.Process.Http.Client.UnitTests;

public sealed class ProcessPublishedHttpRouteTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void RequirePath_AcceptsOnlyARelativeRouteUnderTheSelectedGatewayVersion()
    {
        Assert.Equal("/radiology/2/processes/execute",
            ProcessPublishedHttpRoute.RequirePath("/radiology/2/processes/execute", "/radiology/2"));
    }

    [Theory]
    [InlineData("/radiology/3/processes/execute")]
    [InlineData("/radiology/2/../registry")]
    [InlineData("/radiology/2/%2e%2e/registry")]
    [InlineData("//radiology/2/processes/execute")]
    [InlineData("https://other.example/radiology/2/processes/execute")]
    [InlineData("/radiology/2/processes/execute?key=x")]
    [InlineData("/radiology/2/processes/execute#fragment")]
    [InlineData("/radiology/2//execute")]
    [InlineData("/radiology/2\\processes\\execute")]
    public void RequirePath_RefusesUnsafeOrCrossVersionRoutes(string path)
    {
        var exception = Assert.Throws<KaleidoRegistryClientException>(() => ProcessPublishedHttpRoute.RequirePath(path, "/radiology/2"));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
    }

    [Fact]
    public void ProcessState_ExpandsOnlyThePublishedStateTemplate()
    {
        var processId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal($"/radiology/2/processes/{processId:D}",
            ProcessPublishedHttpRoute.ProcessState("/radiology/2/processes/{processId}", processId, "/radiology/2"));
    }

    [Theory]
    [InlineData("/radiology/2/processes/{wrong}")]
    [InlineData("/radiology/2/processes/{processId}/{processId}")]
    [InlineData("/radiology/3/processes/{processId}")]
    public void ProcessState_RefusesUnpublishedOrAmbiguousPlaceholders(string template)
    {
        var exception = Assert.Throws<KaleidoRegistryClientException>(() =>
            ProcessPublishedHttpRoute.ProcessState(template, Guid.NewGuid(), "/radiology/2"));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
    }
}
