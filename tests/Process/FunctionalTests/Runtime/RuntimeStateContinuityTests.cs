using Kaleido.Process.FunctionalTests.Assets.Runtime;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

[Collection(nameof(RuntimeTestCollection))]
public sealed class RuntimeStateContinuityTests
{
    private readonly IParticipantRuntime _runtime;

    public RuntimeStateContinuityTests(RuntimeTestFixture fixture)
    {
        _runtime = fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessContinuesAcrossMultipleRequests_RemembersPreviouslyCompletedSteps()
    {
        var participantProcessId = Guid.NewGuid();

        var first = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                participantProcessId,
                "request-1",
                RuntimeStepNames.Root));

        RuntimeResultAssert.StepCompleted(first, RuntimeStepNames.Root);
        RuntimeResultAssert.AvailableStep(first, RuntimeStepNames.StepA);
        RuntimeResultAssert.AvailableStep(first, RuntimeStepNames.StepB);

        var second = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                participantProcessId,
                "request-2",
                RuntimeStepNames.StepA));

        RuntimeResultAssert.StepCompleted(second, RuntimeStepNames.StepA);
        RuntimeResultAssert.AvailableStep(second, RuntimeStepNames.StepB);

        var third = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                participantProcessId,
                "request-3",
                RuntimeStepNames.StepB));

        RuntimeResultAssert.StepCompleted(third, RuntimeStepNames.StepB);
        RuntimeResultAssert.AvailableStep(third, RuntimeStepNames.Merge);
    }
}
