using Kaleido.UnitTests;

namespace Kaleido.Processor.Execution.UnitTests;

public sealed class ExecutionDecisionTests
    : SutFixture
{
    private static ExecutionDecision CreateSut() => ExecutionDecision.Complete();

    [Fact]
    public void Continue_CarriesTheNextCandidate()
    {
        var candidate = new StepCandidate { StepName = "CaptureMember" };

        var decision = ExecutionDecision.Continue(candidate);

        Assert.Equal(ExecutionDecisionType.Continue, decision.Type);
        Assert.Same(candidate, decision.NextCandidate);
    }

    [Fact]
    public void TheTerminalOutcomes_CarryOnlyTheirType()
    {
        Assert.Equal(ExecutionDecisionType.Complete, CreateSut().Type);
        Assert.Equal(ExecutionDecisionType.BusinessFailure, ExecutionDecision.BusinessFailure().Type);
        Assert.Equal(ExecutionDecisionType.Rejected, ExecutionDecision.Rejected().Type);
        Assert.All(
            [CreateSut(), ExecutionDecision.BusinessFailure(), ExecutionDecision.Rejected()],
            d => Assert.True(d.NextCandidate is null && d.RequiredStep is null && d.HandOff is null && d.AvailableSteps.Count == 0 && d.Messages.Count == 0));
    }

    [Fact]
    public void ProcessViolation_CarriesItsMessage()
    {
        var message = StepProcessingMessage.Error("pro_violation", "Not now.");

        var decision = ExecutionDecision.ProcessViolation(message);

        Assert.Equal(ExecutionDecisionType.ProcessViolation, decision.Type);
        Assert.Equal([message], decision.Messages);
    }

    [Fact]
    public void AwaitingRequiredStep_CarriesTheStep()
    {
        var decision = ExecutionDecision.AwaitingRequiredStep("CaptureService");

        Assert.Equal(ExecutionDecisionType.AwaitingRequiredStep, decision.Type);
        Assert.Equal("CaptureService", decision.RequiredStep);
        Assert.Null(decision.HandOff);
    }

    [Fact]
    public void AwaitingStepSelection_CarriesTheAvailableSteps()
    {
        var decision = ExecutionDecision.AwaitingStepSelection(["A", "B"]);

        Assert.Equal(ExecutionDecisionType.AwaitingStepSelection, decision.Type);
        Assert.Equal(["A", "B"], decision.AvailableSteps);
    }

    [Fact]
    public void AwaitingInformation_CarriesTheStepAndItsRequest()
    {
        var request = new InformationRequest { InformationRequestId = "questions" };

        var decision = ExecutionDecision.AwaitingInformation("AnswerQuestions", request);

        Assert.Equal(ExecutionDecisionType.AwaitingInformation, decision.Type);
        Assert.Equal("AnswerQuestions", decision.RequiredStep);
        Assert.Same(request, decision.InformationRequest);
    }

    private static readonly HandOffContinuation Radiology =
        new()
        {
            TargetProcessorName = "radiology",
            Succeeded = true,
            AvailableSteps = ["ValidateMemberStep"],
            FrameworkMessages = [new ProcessMessage { Code = "pro_trace", Type = MessageType.Warning, Message = "Slow lookup." }],
        };

    [Fact]
    public void HandOffTo_CarriesTheTargetsAnswer()
    {
        var decision = ExecutionDecision.HandOffTo(Radiology);

        Assert.Equal(ExecutionDecisionType.HandOff, decision.Type);
        Assert.Same(Radiology, decision.HandOff);
    }

    // #198: the target's framework messages become the step's runtime messages, so the transport's
    // framework-message option applies to them as to the origin's own; none is dropped.
    [Fact]
    public void HandOffTo_CarriesTheTargetsFrameworkMessagesAsRuntimeMessages()
    {
        var decision = ExecutionDecision.HandOffTo(Radiology);

        var message = Assert.Single(decision.Messages);
        var expected = Assert.Single(Radiology.FrameworkMessages);
        Assert.Equal((expected.Type, expected.Code, expected.Message), (message.Type, message.Code, message.Message));
    }

    // The one mapping the process state, the answer and the event share (#522, #536): every decision a
    // handler's result can lead to gives its step one status.
    [Fact]
    public void StepStatus_IsTheStatusOfTheStepWhoseResultTheDecisionIs()
    {
        Assert.All(
            [
                ExecutionDecision.Continue(new StepCandidate { StepName = "Next" }),
                CreateSut(),
                ExecutionDecision.HandOffTo(Radiology),
                ExecutionDecision.AwaitingRequiredStep("Next"),
                ExecutionDecision.AwaitingStepSelection(["A"]),
                ExecutionDecision.AwaitingInformation("Answers", new InformationRequest { InformationRequestId = "q" }),
            ],
            d => Assert.Equal(StepExecutionStatus.Completed, d.StepStatus));
        Assert.Equal(StepExecutionStatus.Failed, ExecutionDecision.BusinessFailure().StepStatus);
        Assert.Equal(StepExecutionStatus.Exception, ExecutionDecision.ProcessViolation(StepProcessingMessage.Error("pro_violation", "Not now.")).StepStatus);
        Assert.Equal(StepExecutionStatus.Rejected, ExecutionDecision.Rejected().StepStatus);
    }
}
