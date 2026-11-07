namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryContractUrlsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData(null, "/registry")]
    [InlineData("", "/registry")]
    [InlineData("   ", "/registry")]
    [InlineData("members", "/members/registry")]
    [InlineData(" /members/ ", "/members/registry")]
    public void Registry_NormalizesTheServiceName(string? serviceName, string expected)
    {
        Assert.Equal(expected, RegistryContractUrls.Registry(serviceName!));
    }
}
