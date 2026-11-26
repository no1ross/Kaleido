namespace Kaleido.Http.UnitTests;

public sealed class KaleidoHttpRoutePrefixTests : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData("clinic", "1", "/clinic/1")]
    [InlineData(" /clinic/ ", " /2026-11/ ", "/clinic/2026-11")]
    public void Service_UsesTheVersionedLogicalGatewayRoute(string service, string version, string route)
    {
        Assert.Equal(route, KaleidoHttpRoutePrefix.Service(service, version));
    }

    [Fact]
    public void Service_RequiresBothNameAndVersion()
    {
        Assert.Throws<ArgumentException>(() => KaleidoHttpRoutePrefix.Service(" ", "1"));
        Assert.Throws<ArgumentException>(() => KaleidoHttpRoutePrefix.Service("clinic", " "));
    }
}
