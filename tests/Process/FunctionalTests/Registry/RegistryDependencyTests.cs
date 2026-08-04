using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

[Collection(nameof(RegistryTestCollection))]
public sealed class RegistryDependencyTests
{
    private readonly IProcessStepRegistry _registry;

    public RegistryDependencyTests(RegistryTestFixture fixture)
    {
        _registry =
            fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();
    }

    [Theory]
    [InlineData(typeof(RegistryRootStep), false)]
    [InlineData(typeof(RegistryChildStepA), true)]
    [InlineData(typeof(RegistryChildStepB), true)]
    [InlineData(typeof(RegistryMergeStep), true)]
    [InlineData(typeof(RegistryStandaloneStep), false)]
    public void HasDependencies_ReturnsExpectedValue(
        Type stepType,
        bool expected)
    {
        Assert.Equal(expected, _registry.HasDependencies(stepType));
    }

    [Fact]
    public void GetDependencies_ForRootStep_ReturnsEmptyCollection()
    {
        var dependencies =
            _registry.GetDependencies(typeof(RegistryRootStep));

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependencies_ForStandaloneStep_ReturnsEmptyCollection()
    {
        var dependencies =
            _registry.GetDependencies(typeof(RegistryStandaloneStep));

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependencies_ForChildStepA_ReturnsRootStep()
    {
        var dependencies =
            _registry.GetDependencies(typeof(RegistryChildStepA));

        RegistryAssert.ContainsStepTypes(
            dependencies,
            typeof(RegistryRootStep));
    }

    [Fact]
    public void GetDependencies_ForChildStepB_ReturnsRootStep()
    {
        var dependencies =
            _registry.GetDependencies(typeof(RegistryChildStepB));

        RegistryAssert.ContainsStepTypes(
            dependencies,
            typeof(RegistryRootStep));
    }

    [Fact]
    public void GetDependencies_ForMergeStep_ReturnsChildStepAAndChildStepB()
    {
        var dependencies =
            _registry.GetDependencies(typeof(RegistryMergeStep));

        RegistryAssert.ContainsStepTypes(
            dependencies,
            typeof(RegistryChildStepA),
            typeof(RegistryChildStepB));
    }

    [Fact]
    public void GetDependencyChain_ForMergeStep_ReturnsTransitiveDependencies()
    {
        var dependencyChain =
            _registry.GetDependencyChain(typeof(RegistryMergeStep));

        RegistryAssert.ContainsStepTypes(
            dependencyChain,
            typeof(RegistryChildStepA),
            typeof(RegistryChildStepB),
            typeof(RegistryRootStep));
    }

    [Fact]
    public void GetDependencyChain_ForRootStep_ReturnsEmptyCollection()
    {
        var dependencyChain =
            _registry.GetDependencyChain(typeof(RegistryRootStep));

        Assert.Empty(dependencyChain);
    }
}
