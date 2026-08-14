using Kaleido.Process.FunctionalTests.Assets.Runtime;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

[Collection(nameof(RuntimeTestCollection))]
public sealed class RuntimeUnknownAndAlreadyProcessedTests
{
    private readonly IParticipantRuntime _runtime;

    public RuntimeUnknownAndAlreadyProcessedTests(RuntimeTestFixture fixture)
    {
        _runtime = fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownStepIsSubmitted_ReturnsUnknownStepMessage()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid(),
                "request-1",
                RuntimeStepNames.Root,
                "TotallyFakeStep"));

        RuntimeResultAssert.HasMessage(result, StepProcessingMessageCode.UnknownStep);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreviouslyCompletedStepIsSubmittedAgain_ReturnsAlreadyProcessedMessage()
    {
        var participantProcessId = Guid.NewGuid();

        await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                participantProcessId,
                "request-1",
                RuntimeStepNames.Root));

        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                participantProcessId,
                "request-2",
                RuntimeStepNames.Root));

        RuntimeResultAssert.HasMessage(result, StepProcessingMessageCode.AlreadyProcessed);
    }
}
