using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Kaleido.Process.Participant.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

[Collection(nameof(RegistryTestCollection))]
public sealed class RegistryBootstrapTests
{
    private readonly RegistryTestFixture _fixture;

    public RegistryBootstrapTests(RegistryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CanResolveProcessStepRegistry()
    {
        var registry =
            _fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();

        Assert.NotNull(registry);
    }

    [Fact]
    public void CanResolveCoreFrameworkServices()
    {
        Assert.NotNull(_fixture.ServiceProvider.GetRequiredService<IExecutionPlanner>());
        Assert.NotNull(_fixture.ServiceProvider.GetRequiredService<IExecutionProcessor>());
        Assert.NotNull(_fixture.ServiceProvider.GetRequiredService<IProcessStepInvoker>());
        Assert.NotNull(_fixture.ServiceProvider.GetRequiredService<IProcessStateUpdater>());
        Assert.NotNull(_fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>());
    }

    [Fact]
    public void Registrations_ContainsExpectedNumberOfSteps()
    {
        var registry =
            _fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();

        Assert.Equal(5, registry.Registrations.Count);
    }

    [Fact]
    public void Registrations_ContainsExpectedStepTypes()
    {
        var registry =
            _fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();

        RegistryAssert.ContainsStepTypes(
            registry.Registrations,
            typeof(RegistryRootStep),
            typeof(RegistryChildStepA),
            typeof(RegistryChildStepB),
            typeof(RegistryMergeStep),
            typeof(RegistryStandaloneStep));
    }
}
