using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Registry;
using Moq;

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
            .ReturnsAsync((ProcessorContext?)null);

        var service =
            new ProcessStateService(
                contextStore.Object,
                CreateRegistry(),
                new ProcessRouteOptions());

        var result =
            await service.GetCurrentState(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_MapsView()
    {
        var processId =
            Guid.NewGuid();

        var context =
            new ProcessorContext
            {
                ProcessId = processId,
                ProcessorName = "test-processor",
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
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var service =
            new ProcessStateService(
                contextStore.Object,
                CreateRegistry("Step-A"),
                new ProcessRouteOptions());

        var result =
            await service.GetCurrentState(
                processId,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal("Step-B", result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Equal("Step-A", Assert.Single(result.AvailableSteps).Name);

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

    private static IProcessStepRegistry CreateRegistry(
        params string[] stepNames)
    {
        var mock = new Mock<IProcessStepRegistry>();

        foreach (var name in stepNames)
        {
            var registration = new ProcessStepRegistration(
                typeof(object),
                null,
                typeof(object),
                [],
                [],
                [],
                new RepeatableOptions { Enabled = false },
                new ProcessStepMetadata(name, name, "1.0.0", name));

            mock.Setup(x => x.Find(name))
                .Returns(registration);
        }

        return mock.Object;
    }
}
