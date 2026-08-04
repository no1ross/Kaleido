using Kaleido.Process.FunctionalTests.Assets.Registry;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

[Collection(nameof(RegistryTestCollection))]
public sealed class RegistryRegistrationTests
{
    private readonly IProcessStepRegistry _registry;

    public RegistryRegistrationTests(RegistryTestFixture fixture)
    {
        _registry =
            fixture.ServiceProvider.GetRequiredService<IProcessStepRegistry>();
    }

    [Theory]
    [InlineData(nameof(RegistryRootStep), typeof(RegistryRootStep))]
    [InlineData(nameof(RegistryChildStepA), typeof(RegistryChildStepA))]
    [InlineData(nameof(RegistryChildStepB), typeof(RegistryChildStepB))]
    [InlineData(nameof(RegistryMergeStep), typeof(RegistryMergeStep))]
    [InlineData(nameof(RegistryStandaloneStep), typeof(RegistryStandaloneStep))]
    public void Find_ByName_WhenRegistrationExists_ReturnsRegistration(
        string name,
        Type expectedStepType)
    {
        var registration =
            _registry.Find(name);

        Assert.NotNull(registration);
        Assert.Equal(expectedStepType, registration.StepType);
    }

    [Fact]
    public void Find_ByName_WhenRegistrationDoesNotExist_ReturnsNull()
    {
        var registration =
            _registry.Find("UnknownStep");

        Assert.Null(registration);
    }

    [Theory]
    [InlineData(typeof(RegistryRootStep))]
    [InlineData(typeof(RegistryChildStepA))]
    [InlineData(typeof(RegistryChildStepB))]
    [InlineData(typeof(RegistryMergeStep))]
    [InlineData(typeof(RegistryStandaloneStep))]
    public void Find_ByType_WhenRegistrationExists_ReturnsRegistration(Type stepType)
    {
        var registration =
            _registry.Find(stepType);

        Assert.NotNull(registration);
        Assert.Equal(stepType, registration.StepType);
    }

    [Fact]
    public void Find_ByType_WhenRegistrationDoesNotExist_ReturnsNull()
    {
        var registration =
            _registry.Find(typeof(string));

        Assert.Null(registration);
    }

    [Fact]
    public void GetRegistration_ByName_WhenRegistrationExists_ReturnsRegistration()
    {
        var registration =
            _registry.GetRegistration(nameof(RegistryMergeStep));

        Assert.Equal(typeof(RegistryMergeStep), registration.StepType);
    }

    [Fact]
    public void GetRegistration_ByName_WhenRegistrationDoesNotExist_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            _registry.GetRegistration("UnknownStep"));
    }

    [Fact]
    public void GetRegistration_ByType_WhenRegistrationExists_ReturnsRegistration()
    {
        var registration =
            _registry.GetRegistration(typeof(RegistryMergeStep));

        Assert.Equal(typeof(RegistryMergeStep), registration.StepType);
    }

    [Fact]
    public void GetRegistration_ByType_WhenRegistrationDoesNotExist_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            _registry.GetRegistration(typeof(string)));
    }

    [Theory]
    [InlineData(typeof(RegistryRootStep), typeof(RegistryRootStepHandler), typeof(RegistryRootStepResponse))]
    [InlineData(typeof(RegistryChildStepA), typeof(RegistryChildStepAHandler), typeof(RegistryChildStepAResponse))]
    [InlineData(typeof(RegistryChildStepB), typeof(RegistryChildStepBHandler), typeof(RegistryChildStepBResponse))]
    [InlineData(typeof(RegistryMergeStep), typeof(RegistryMergeStepHandler), typeof(RegistryMergeStepResponse))]
    [InlineData(typeof(RegistryStandaloneStep), typeof(RegistryStandaloneStepHandler), typeof(RegistryStandaloneStepResponse))]
    public void Registration_ContainsExpectedHandlerAndResponseTypes(
        Type stepType,
        Type expectedHandlerType,
        Type expectedStepResultType)
    {
        var registration =
            _registry.GetRegistration(stepType);

        Assert.Equal(expectedHandlerType, registration.HandlerType);
        Assert.Equal(expectedStepResultType, registration.StepResultType);
    }
}
