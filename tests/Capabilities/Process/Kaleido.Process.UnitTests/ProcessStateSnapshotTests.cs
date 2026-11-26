using Kaleido.Observability;
using Kaleido.Process.Context;

namespace Kaleido.Process.UnitTests;

public sealed class ProcessStateSnapshotTests : Kaleido.UnitTests.SutFixture<ProcessStateSnapshot>
{
    protected override ProcessStateSnapshot CreateSut() => new()
    {
        ProcessId = Guid.NewGuid(),
        ServiceName = "catalog",
    };

    [Fact]
    public void FromContext_KeepsProcessAndStepStateWithoutStoreOnlyMetadata()
    {
        var processId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var caller = new KaleidoCaller { Name = "alice", Id = "alice-1" };
        var questions = new InformationRequest { InformationRequestId = "confirm", ExchangeId = "exchange-1" };
        var context = new ProcessContext
        {
            ProcessId = processId,
            ProcessorName = "catalog",
            ProcessorVersion = "1",
            Version = 17,
            LatestRequestId = "internal-request",
            State = ProcessExecutionState.AwaitingInformation,
            RequiredStep = "ConfirmStep",
            RequiredInformationRequest = questions,
            AvailableSteps = ["NextStep"],
            Steps =
            [
                new StepContext { StepName = "ZuluStep", Status = StepExecutionStatus.Pending },
                new StepContext { StepName = "AlphaStep", Status = StepExecutionStatus.Completed, FirstExecuted = now, LastExecuted = now, StartedBy = caller, LastUpdatedBy = caller, LatestRequestId = "private-step-request" },
            ],
            CreatedUtc = now,
            UpdatedUtc = now,
            CreatedBy = caller,
            UpdatedBy = caller,
        };

        var snapshot = ProcessStateSnapshot.FromContext(context);

        Assert.Equal((processId, "catalog", "1"), (snapshot.ProcessId, snapshot.ServiceName, snapshot.ServiceVersion));
        Assert.Equal(ProcessExecutionState.AwaitingInformation, snapshot.State);
        Assert.Equal("ConfirmStep", snapshot.RequiredStep);
        Assert.Same(questions, snapshot.RequiredInformationRequest);
        Assert.Equal(["NextStep"], snapshot.AvailableSteps);
        Assert.Equal(["AlphaStep", "ZuluStep"], snapshot.Steps.Select(step => step.StepName));
        var step = snapshot.Steps.First();
        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal(now, step.FirstExecuted);
        Assert.Same(caller, step.StartedBy);
        Assert.Equal((now, now), (snapshot.CreatedUtc, snapshot.UpdatedUtc));
        Assert.Same(caller, snapshot.CreatedBy);
        Assert.DoesNotContain(typeof(ProcessStateSnapshot).GetProperties(), property => property.Name is "Version" or "LatestRequestId" or "ExecuteUrl" or "Bindings");
        Assert.DoesNotContain(typeof(ProcessStateStep).GetProperties(), property => property.Name is "LatestRequestId" or "ExecuteUrl" or "Bindings");
    }

    [Fact]
    public void FromContext_AHandOffKeepsTheTargetVersionForLaterStateReads()
    {
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "origin",
            ProcessorVersion = "1",
            State = ProcessExecutionState.HandOff,
            TargetProcessorName = "target",
            TargetProcessorVersion = "2",
        };

        var snapshot = ProcessStateSnapshot.FromContext(context);

        Assert.Equal("1", snapshot.ServiceVersion);
        Assert.Equal("target", snapshot.TargetProcessorName);
        Assert.Equal("2", snapshot.TargetProcessorVersion);
    }
}
