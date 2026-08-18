using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessStateServiceTests
{
    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_ReturnsNull()
    {
        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParticipantContext?)null);

        var service =
            new ProcessStateService(
                contextStore.Object);

        var result =
            await service.GetCurrentState(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_MapsView()
    {
        var participantProcessId =
            Guid.NewGuid();

        var context =
            new ParticipantContext
            {
                ParticipantProcessId = participantProcessId,
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = "Step-B",
                AvailableSteps = ["Step-A"],
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedUtc = DateTimeOffset.UtcNow,
                Steps =
                [
                    new StepContext
                    {
                        StepName = "Step-B",
                        Version = "1.0.0",
                        Status = StepExecutionStatus.Pending
                    },
                    new StepContext
                    {
                        StepName = "Step-A",
                        Version = "1.0.0",
                        Status = StepExecutionStatus.Completed,
                        LastExecuted = DateTimeOffset.UtcNow.AddMinutes(-1)
                    }
                ]
            };

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    participantProcessId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var service =
            new ProcessStateService(
                contextStore.Object);

        var result =
            await service.GetCurrentState(
                participantProcessId,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(participantProcessId, result.ParticipantProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal("Step-B", result.RequiredStep);
        Assert.Equal("Step-A", Assert.Single(result.AvailableSteps));

        Assert.Collection(
            result.Steps,
            step =>
            {
                Assert.Equal("Step-A", step.StepName);
                Assert.Equal(StepExecutionStatus.Completed, step.Status);
            },
            step =>
            {
                Assert.Equal("Step-B", step.StepName);
                Assert.Equal(StepExecutionStatus.Pending, step.Status);
            });
    }
}
