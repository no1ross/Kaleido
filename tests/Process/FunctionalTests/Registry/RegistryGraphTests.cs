using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

[Collection(nameof(RegistryTestCollection))]
public sealed class RegistryGraphTests
{
    private readonly IProcessStepRegistry _registry;

    public RegistryGraphTests(RegistryTestFixture fixture)
    {
        _registry =
            fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();
    }

    [Fact]
    public void Graph_IsAvailable()
    {
        Assert.NotNull(_registry.Graph);
    }

    [Fact]
    public void RegistryRootStep_IsGraphRoot()
    {
        Assert.False(_registry.HasDependencies(typeof(RegistryRootStep)));
        Assert.True(_registry.HasDependents(typeof(RegistryRootStep)));
    }

    [Fact]
    public void RegistryMergeStep_IsGraphLeaf()
    {
        Assert.True(_registry.HasDependencies(typeof(RegistryMergeStep)));
        Assert.False(_registry.HasDependents(typeof(RegistryMergeStep)));
    }

    [Fact]
    public void RegistryStandaloneStep_IsValidDisconnectedStep()
    {
        Assert.False(_registry.HasDependencies(typeof(RegistryStandaloneStep)));
        Assert.False(_registry.HasDependents(typeof(RegistryStandaloneStep)));
        Assert.Empty(_registry.GetDependencies(typeof(RegistryStandaloneStep)));
        Assert.Empty(_registry.GetDependents(typeof(RegistryStandaloneStep)));
    }
}
