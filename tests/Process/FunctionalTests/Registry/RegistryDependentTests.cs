using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

[Collection(nameof(RegistryTestCollection))]
public sealed class RegistryDependentTests
{
    private readonly IProcessStepRegistry _registry;

    public RegistryDependentTests(RegistryTestFixture fixture)
    {
        _registry =
            fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();
    }

    [Theory]
    [InlineData(typeof(RegistryRootStep), true)]
    [InlineData(typeof(RegistryChildStepA), true)]
    [InlineData(typeof(RegistryChildStepB), true)]
    [InlineData(typeof(RegistryMergeStep), false)]
    [InlineData(typeof(RegistryStandaloneStep), false)]
    public void HasDependents_ReturnsExpectedValue(
        Type stepType,
        bool expected)
    {
        Assert.Equal(expected, _registry.HasDependents(stepType));
    }

    [Fact]
    public void GetDependents_ForRootStep_ReturnsChildStepAAndChildStepB()
    {
        var dependents =
            _registry.GetDependents(typeof(RegistryRootStep));

        RegistryAssert.ContainsStepTypes(
            dependents,
            typeof(RegistryChildStepA),
            typeof(RegistryChildStepB));
    }

    [Fact]
    public void GetDependents_ForChildStepA_ReturnsMergeStep()
    {
        var dependents =
            _registry.GetDependents(typeof(RegistryChildStepA));

        RegistryAssert.ContainsStepTypes(
            dependents,
            typeof(RegistryMergeStep));
    }

    [Fact]
    public void GetDependents_ForChildStepB_ReturnsMergeStep()
    {
        var dependents =
            _registry.GetDependents(typeof(RegistryChildStepB));

        RegistryAssert.ContainsStepTypes(
            dependents,
            typeof(RegistryMergeStep));
    }

    [Fact]
    public void GetDependents_ForMergeStep_ReturnsEmptyCollection()
    {
        var dependents =
            _registry.GetDependents(typeof(RegistryMergeStep));

        Assert.Empty(dependents);
    }

    [Fact]
    public void GetDependents_ForStandaloneStep_ReturnsEmptyCollection()
    {
        var dependents =
            _registry.GetDependents(typeof(RegistryStandaloneStep));

        Assert.Empty(dependents);
    }

    [Fact]
    public void GetDependentChain_ForRootStep_ReturnsTransitiveDependents()
    {
        var dependentChain =
            _registry.GetDependentChain(typeof(RegistryRootStep));

        RegistryAssert.ContainsStepTypes(
            dependentChain,
            typeof(RegistryChildStepA),
            typeof(RegistryChildStepB),
            typeof(RegistryMergeStep));
    }

    [Fact]
    public void GetDependentChain_ForMergeStep_ReturnsEmptyCollection()
    {
        var dependentChain =
            _registry.GetDependentChain(typeof(RegistryMergeStep));

        Assert.Empty(dependentChain);
    }
}
