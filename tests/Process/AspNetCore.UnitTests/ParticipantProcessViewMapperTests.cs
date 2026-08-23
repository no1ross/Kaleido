using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ParticipantProcessViewMapperTests
{
    [Fact]
    public void ToView_MapsProcessStateAndOrdersStepsByName()
    {
        var processId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedUtc = DateTimeOffset.UtcNow;
        var stepBLastExecuted = DateTimeOffset.UtcNow.AddMinutes(-2);

        var context =
            new ParticipantContext
            {
                ProcessId = processId,
                LatestRequestId = "REQ-001",
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = "Step-B",
                AvailableSteps = ["Step-B", "Step-C"],
                CreatedUtc = createdUtc,
                UpdatedUtc = updatedUtc,
                Steps =
                [
                    new StepContext
                    {
                        StepName = "Step-B",
                        Version = "1.0.0",
                        Status = StepExecutionStatus.Pending,
                        LastExecuted = stepBLastExecuted
                    },
                    new StepContext
                    {
                        StepName = "Step-A",
                        Version = "1.1.0",
                        Status = StepExecutionStatus.Completed
                    }
                ]
            };

        var result =
            ParticipantProcessViewMapper.ToView(context);

        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal("Step-B", result.RequiredStep);
        Assert.Equal(createdUtc, result.CreatedUtc);
        Assert.Equal(updatedUtc, result.UpdatedUtc);
        Assert.Equal(context.AvailableSteps, result.AvailableSteps);

        Assert.Collection(
            result.Steps,
            step =>
            {
                Assert.Equal("Step-A", step.StepName);
                Assert.Equal("1.1.0", step.Version);
                Assert.Equal(StepExecutionStatus.Completed, step.Status);
                Assert.Null(step.LastExecuted);
            },
            step =>
            {
                Assert.Equal("Step-B", step.StepName);
                Assert.Equal("1.0.0", step.Version);
                Assert.Equal(StepExecutionStatus.Pending, step.Status);
                Assert.Equal(stepBLastExecuted, step.LastExecuted);
            });
    }
}
