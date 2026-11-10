namespace Kaleido.Processor.Execution.UnitTests;

// The untyped result and the factories every handler starts from. The typed result's own members
// (Next, RequireInformation, the conversion) are in ProcessStepHandlerResultOfTTests.
public sealed class ProcessStepHandlerResultTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ProcessMessage Note =
        new() { Code = "note", Type = MessageType.Information, Message = "A note." };

    private static readonly InformationRequest Request =
        new() { InformationRequestId = "questions" };

    private static readonly ProcessMessage Diagnostic =
        new() { Code = "trace", Type = MessageType.Warning, Message = "A diagnostic." };

    private static ProcessStepHandlerResult CreateSut() => ProcessStepHandlerResult.Success(Note);

    // The target's answer as the Kaleido client would hand it over (#198).
    private static IHandOffContinuationSource Answer(HandOffContinuation continuation)
    {
        var answer = new Mock<IHandOffContinuationSource>();
        answer.Setup(x => x.ToHandOffContinuation(It.IsAny<string>())).Returns(continuation);
        return answer.Object;
    }

    private static readonly HandOffContinuation BillingAnswer =
        new()
        {
            TargetProcessorName = "billing",
            Succeeded = true,
            BusinessMessages = [Note],
            FrameworkMessages = [Diagnostic],
            AvailableSteps = ["SendInvoiceStep"],
        };

    [Fact]
    public void Success_IsASuccessWithoutARequiredStep()
    {
        var result = CreateSut();

        Assert.True(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.HandOffContinuation);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void SuccessWithNextStep_RequiresThatStep()
    {
        var result = ProcessStepHandlerResult.Success<NextStep>(Note);

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(NextStep), result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_RequiresTheInformationStepWithItsRequest()
    {
        var result = ProcessStepHandlerResult.RequireInformation<AnswerStep>(Request, Note);

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_WithoutARequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessStepHandlerResult.RequireInformation<AnswerStep>(null!));
    }

    [Fact]
    public void Failure_IsANonRejectedFailureWithItsMessages()
    {
        var result = ProcessStepHandlerResult.Failure(Note);

        Assert.False(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.Response);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void Complete_IsASuccessThatCompletesTheProcess()
    {
        var result = ProcessStepHandlerResult.Complete(Note);

        Assert.True(result.Succeeded);
        Assert.True(result.CompletesProcess);
        Assert.False(result.Rejected);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.HandOffContinuation);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void CompleteWithResponse_IsATypedSuccessThatCompletesTheProcess()
    {
        var result = ProcessStepHandlerResult.Complete("closed", Note);

        Assert.True(result.Succeeded);
        Assert.True(result.CompletesProcess);
        Assert.Equal("closed", result.Response);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void OtherResults_DoNotCompleteTheProcess()
    {
        Assert.All(
            [CreateSut(), ProcessStepHandlerResult.Success<NextStep>(), ProcessStepHandlerResult.Failure(), ProcessStepHandlerResult.Reject(), ProcessStepHandlerResult.HandOff("billing", Answer(BillingAnswer))],
            result => Assert.False(result.CompletesProcess));
    }

    [Fact]
    public void Reject_IsARejectedFailure()
    {
        var result = ProcessStepHandlerResult.Reject(Note);

        Assert.False(result.Succeeded);
        Assert.True(result.Rejected);
        Assert.Equal([Note], result.Messages);
    }

    // #198: a hand-off carries the target's answer; the step's business messages are the target's,
    // and the local required step stays unset (the target's next steps are by name, in the answer).
    [Fact]
    public void HandOff_IsASuccessCarryingTheTargetsAnswer()
    {
        var result = ProcessStepHandlerResult.HandOff("billing", Answer(BillingAnswer));

        Assert.True(result.Succeeded);
        Assert.Equal(BillingAnswer, result.HandOffContinuation);
        Assert.Null(result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    // The target is the one the handler named, whatever its answer calls itself.
    [Fact]
    public void HandOff_NamesTheTargetTheHandlerHandedOffTo()
    {
        var result = ProcessStepHandlerResult.HandOff("billing", Answer(BillingAnswer with { TargetProcessorName = "elsewhere" }));

        Assert.Equal("billing", result.HandOffContinuation?.TargetProcessorName);
    }

    // A typed step hands off with its own response, built by the handler, next to the target's answer.
    [Fact]
    public void HandOffWithResponse_IsATypedSuccess_CarryingItsResponseAndTheTargetsAnswer()
    {
        var result = ProcessStepHandlerResult.HandOff("billing", Answer(BillingAnswer), new Response("case-1"));

        Assert.True(result.Succeeded);
        Assert.Equal("case-1", result.Response?.Value);
        Assert.Equal(BillingAnswer, result.HandOffContinuation);
        Assert.Equal([Note], result.Messages);
    }

    // A handoff decides what comes next: the target's steps, not a local one.
    [Fact]
    public void HandOffWithResponse_CannotRequireALocalStepNext()
    {
        var result = ProcessStepHandlerResult.HandOff("billing", Answer(BillingAnswer), new Response("case-1"));

        Assert.Throws<Kaleido.Exceptions.KaleidoFrameworkException>(result.Next<NextStep>);
    }

    [Fact]
    public void HandOffWithResponse_WithoutAResponse_IsRefused()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ProcessStepHandlerResult.HandOff<Response>("billing", Answer(BillingAnswer), null!));

        Assert.Equal("response", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HandOffWithResponse_ToAnEmptyOrBlankTarget_IsRefused(string target)
    {
        var exception = Assert.Throws<ArgumentException>(() => ProcessStepHandlerResult.HandOff(target, Answer(BillingAnswer), new Response("case-1")));

        Assert.Equal("targetProcessorName", exception.ParamName);
    }

    [Fact]
    public void HandOff_WithoutTheTargetsAnswer_IsRefused()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ProcessStepHandlerResult.HandOff("billing", null!));

        Assert.Equal("targetResponse", exception.ParamName);
    }

    // A handoff names a service: an empty or blank target used to degrade silently into a local
    // continuation or a handoff to nowhere (#522).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HandOff_ToAnEmptyOrBlankTarget_IsRefused(string target)
    {
        var exception = Assert.Throws<ArgumentException>(() => ProcessStepHandlerResult.HandOff(target, Answer(BillingAnswer)));

        Assert.Equal("targetProcessorName", exception.ParamName);
    }

    [Fact]
    public void HandOff_ToANullTarget_IsRefused()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ProcessStepHandlerResult.HandOff(null!, Answer(BillingAnswer)));

        Assert.Equal("targetProcessorName", exception.ParamName);
    }

    [Fact]
    public void SuccessWithResponse_IsATypedSuccess()
    {
        var result = ProcessStepHandlerResult.Success(new Response("ok"), Note);

        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Response?.Value);
        Assert.Null(result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void FailureWithResponse_IsATypedNonRejectedFailure()
    {
        var result = ProcessStepHandlerResult.Failure(new Response("no"), Note);

        Assert.False(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Equal("no", result.Response?.Value);
        Assert.Equal([Note], result.Messages);
    }

    private sealed record Response(string Value);

    private sealed record NextStep : IProcessStep;

    private sealed record AnswerStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }
        public string ExchangeId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
