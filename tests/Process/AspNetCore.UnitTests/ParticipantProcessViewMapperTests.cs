using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Registry;
using Moq;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessorProcessViewMapperTests
{
    private const string LocalProcessorName = "test-processor";

    [Fact]
    public void ToView_MapsProcessStateAndOrdersStepsByName()
    {
        var processId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedUtc = DateTimeOffset.UtcNow;
        var stepBLastExecuted = DateTimeOffset.UtcNow.AddMinutes(-2);

        var context =
            new ProcessorContext
            {
                ProcessId = processId,
                ProcessorName = "test-processor",
                LatestRequestId = "REQ-001",
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = new ProcessStepReference
                {
                    ProcessorName = LocalProcessorName,
                    StepName = "Step-B"
                },
                AvailableSteps =
                [
                    new ProcessStepReference { ProcessorName = LocalProcessorName, StepName = "Step-B" },
                    new ProcessStepReference { ProcessorName = LocalProcessorName, StepName = "Step-C" }
                ],
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
            ProcessorProcessViewMapper.ToView(
                context,
                CreateRegistry(),
                new ProcessRouteOptions());

        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal(createdUtc, result.CreatedUtc);
        Assert.Equal(updatedUtc, result.UpdatedUtc);

        Assert.NotNull(result.RequiredStep);
        Assert.Equal("Step-B", result.RequiredStep.StepName);
        Assert.Equal(LocalProcessorName, result.RequiredStep.ProcessorName);

        Assert.Equal(2, result.AvailableSteps.Count);
        Assert.Contains(result.AvailableSteps, x => x.StepName == "Step-B");
        Assert.Contains(result.AvailableSteps, x => x.StepName == "Step-C");

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

    [Fact]
    public void ToView_WhenRequiredStepIsNull_MapsNullRequiredStep()
    {
        var context =
            new ProcessorContext
            {
                ProcessId = Guid.NewGuid(),
                ProcessorName = "test-processor",
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = null,
                AvailableSteps = []
            };

        var result =
            ProcessorProcessViewMapper.ToView(
                context,
                CreateRegistry(),
                new ProcessRouteOptions());

        Assert.Null(result.RequiredStep);
        Assert.Empty(result.AvailableSteps);
    }

    private static IProcessStepRegistry CreateRegistry()
    {
        var mock = new Mock<IProcessStepRegistry>();
        mock.Setup(x => x.Find(It.IsAny<string>()))
            .Returns((ProcessStepRegistration?)null);
        return mock.Object;
    }
}
