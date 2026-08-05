using Kaleido.Process.FunctionalTests.Assets.Runtime;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

[Collection(nameof(RuntimeTestCollection))]
public sealed class RuntimeDependencyViolationTests
{
    private readonly IParticipantRuntime _runtime;

    public RuntimeDependencyViolationTests(RuntimeTestFixture fixture)
    {
        _runtime = fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMergeStepIsSubmittedBeforeDependencies_ReturnsDependencyMessage()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid().ToString("N"),
                "request-1",
                RuntimeStepNames.Root,
                RuntimeStepNames.Merge));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.Root);
        RuntimeResultAssert.HasMessage(result, StepProcessingMessageCode.DependencyNotSatisfied);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMergeStepHasOnlyOneDependencySatisfied_ReturnsDependencyMessage()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid().ToString("N"),
                "request-1",
                RuntimeStepNames.Root,
                RuntimeStepNames.StepA,
                RuntimeStepNames.Merge));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.Root);
        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.StepA);
        RuntimeResultAssert.HasMessage(result, StepProcessingMessageCode.DependencyNotSatisfied);
    }
}
