using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

public sealed class RegistryRepeatableTests
    : IClassFixture<RegistryTestFixture>
{
    private readonly RegistryTestFixture _fixture;

    public RegistryRepeatableTests(
        RegistryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void RepeatableStep_IsRegisteredAsRepeatable()
    {
        var registration =
            _fixture.Registry.GetRegistration(
                typeof(RegistryRepeatableStep));

        Assert.True(
            registration.Repeatable.Enabled);
    }

    [Fact]
    public void NonRepeatableStep_IsNotRegisteredAsRepeatable()
    {
        var registration =
            _fixture.Registry.GetRegistration(
                typeof(RegistryRootStep));

        Assert.False(
            registration.Repeatable.Enabled);
    }
}