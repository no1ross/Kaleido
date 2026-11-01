using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Kaleido.UnitTests;

namespace Kaleido.Http.Processor.UnitTests;

// #198: a handler hands off with the Kaleido client's step response; it converts itself into the
// core answer the runtime carries.
public sealed class StepExecutionResponseTests
    : SutFixture<StepExecutionResponse>
{
    private static readonly ProcessMessage Started = new() { Code = "started", Type = MessageType.Information, Message = "Radiology intake started." };

    private static readonly ProcessMessage Slow = new() { Code = "pro_trace", Type = MessageType.Warning, Message = "Slow lookup." };

    private static readonly InformationRequest Questions = new() { InformationRequestId = "mri-intake", ExchangeId = "exchange-1" };

    private static readonly KaleidoNotice Withheld = new("pro_not_permitted", "You do not have access to continue this process.");

    protected override StepExecutionResponse CreateSut() =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            StepName = "StartRadiologyIntakeStep",
            Status = StepExecutionStatus.Completed,
            RequiredStep = new ProcessRequiredStep { Name = "CaptureMriInfoStep", InformationRequest = Questions },
            AvailableSteps = [new ProcessStepSummary { Name = "ValidateMemberStep" }, new ProcessStepSummary { Name = "CaptureMemberStep" }],
            Notice = Withheld,
            BusinessMessages = [Started],
            FrameworkMessages = [Slow],
        };

    private static HandOffContinuation Convert(StepExecutionResponse response, string target = "radiology") =>
        ((IHandOffContinuationSource)response).ToHandOffContinuation(target);

    [Fact]
    public void ToHandOffContinuation_CarriesTheAnswer_ByName()
    {
        var continuation = Convert(Sut);

        Assert.Equal("radiology", continuation.TargetProcessorName);
        Assert.True(continuation.Succeeded);
        Assert.False(continuation.Rejected);
        Assert.Equal("CaptureMriInfoStep", continuation.RequiredStep);
        Assert.Same(Questions, continuation.InformationRequest);
        Assert.Equal(["ValidateMemberStep", "CaptureMemberStep"], continuation.AvailableSteps);
        Assert.Same(Withheld, continuation.Notice);
        Assert.Equal([Started], continuation.BusinessMessages);
        Assert.Equal([Slow], continuation.FrameworkMessages);
    }

    [Fact]
    public void ToHandOffContinuation_WithoutARequiredStep_HasNoneAndNoQuestions()
    {
        var continuation = Convert(Sut with { RequiredStep = null });

        Assert.Null(continuation.RequiredStep);
        Assert.Null(continuation.InformationRequest);
    }

    // Only a completed step means the target took the process over.
    [Theory]
    [InlineData(nameof(StepExecutionStatus.Completed), true, false)]
    [InlineData(nameof(StepExecutionStatus.Rejected), false, true)]
    [InlineData(nameof(StepExecutionStatus.Failed), false, false)]
    [InlineData(nameof(StepExecutionStatus.ValidationFailed), false, false)]
    [InlineData(nameof(StepExecutionStatus.Pending), false, false)]
    public void ToHandOffContinuation_TheOutcome_IsTheTargetStepsStatus(string status, bool succeeded, bool rejected)
    {
        var continuation = Convert(Sut with { Status = Enum.Parse<StepExecutionStatus>(status) });

        Assert.Equal((succeeded, rejected), (continuation.Succeeded, continuation.Rejected));
    }
}
