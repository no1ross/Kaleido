namespace Kaleido.Http.Registry.UnitTests;

// #553: the control registry's URLs, shared by its endpoints and the participants that call them.
public sealed class ControlRegistryContractUrlsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly Guid InstanceId = Guid.Parse("4b8d3c1e-2f6a-4c7b-9e0d-1a2b3c4d5e6f");

    [Fact]
    public void TheUrls_AreUnderTheRegistryGroup()
    {
        Assert.Equal("/registry", ControlRegistryContractUrls.Registry);
        Assert.Equal("/registry/status", ControlRegistryContractUrls.Status);
        Assert.Equal("/registry/capabilities", ControlRegistryContractUrls.Capabilities);
        Assert.Equal("/registry/instances", ControlRegistryContractUrls.Instances);
        Assert.Equal("/registry/instances/4b8d3c1e-2f6a-4c7b-9e0d-1a2b3c4d5e6f", ControlRegistryContractUrls.Instance(InstanceId));
        Assert.Equal("/registry/instances/4b8d3c1e-2f6a-4c7b-9e0d-1a2b3c4d5e6f/heartbeat", ControlRegistryContractUrls.Heartbeat(InstanceId));
    }
}
