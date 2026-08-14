using Kaleido.Process.FunctionalTests.Assets.Runtime;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

[Collection(nameof(RuntimeTestCollection))]
public sealed class RuntimeRequiredStepTests
{
    private readonly IParticipantRuntime _runtime;

    public RuntimeRequiredStepTests(RuntimeTestFixture fixture)
    {
        _runtime = fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequiredStepIsAlreadySupplied_ExecutesRequiredStepWithoutAnotherRequest()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid(),
                "request-1",
                RuntimeStepNames.RequiredRoot,
                RuntimeStepNames.RequiredStep));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.RequiredRoot);
        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.RequiredStep);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequiredStepIsNotSupplied_ReturnsAwaitingRequiredStep()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid(),
                "request-1",
                RuntimeStepNames.RequiredRoot));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.RequiredRoot);

        Assert.Equal(
            ProcessExecutionState.AwaitingRequiredStep,
            result.State);

        Assert.Equal(
            RuntimeStepNames.RequiredStep,
            result.RequiredStep);

        Assert.Empty(
            result.AvailableSteps);

    }

    [Fact]
    public async Task ExecuteAsync_WhenRequiredStepIsNotAvailable_ReturnsProcessViolationMessage()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid(),
                "request-1",
                RuntimeStepNames.InvalidRequiredRoot));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.InvalidRequiredRoot);

        Assert.Equal(ProcessExecutionState.ProcessViolation, result.State);
        Assert.Contains(
            result.Steps.SelectMany(x => x.RuntimeMessages),
            x => x.Code == StepProcessingMessageCode.RequiredStepNotAllowed ||
                 x.Code == StepProcessingMessageCode.InvalidRequiredStep);
    }
}
