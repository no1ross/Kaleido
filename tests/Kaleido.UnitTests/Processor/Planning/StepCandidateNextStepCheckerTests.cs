using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Planning.UnitTests;

public sealed class StepCandidateNextStepCheckerTests
    : SutFixture
{
    private static readonly InformationRequest PendingRequest =
        new()
        {
            InformationRequestId = "out-of-network",
            Items = [new InformationItem { Id = "q1", Text = "Proceed?", Type = InformationItemType.Boolean }]
        };

    private static StepCandidateNextStepChecker CreateSut(
        IReadOnlyCollection<string>? availableNow = null,
        IInformationValidator? informationValidator = null)
    {
        var resolver = new Mock<IStepAvailabilityResolver>();
        resolver
            .Setup(x => x.ResolveCurrent(It.IsAny<ProcessorContext>()))
            .Returns(availableNow ?? []);

        return new StepCandidateNextStepChecker(
            resolver.Object,
            informationValidator ?? Mock.Of<IInformationValidator>());
    }

    [Fact]
    public void Check_WhenFirstStepIsAvailable_AdmitsIt()
    {
        var candidate = Candidate<StepA>("step-a");

        CreateSut(["step-a"]).Check([candidate], Context());

        Assert.True(candidate.IncludedInExecutionPlan);
        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
    }

    [Fact]
    public void Check_WhenFirstStepIsNotAvailable_RejectsIt()
    {
        var candidate = Candidate<StepB>("step-b");

        CreateSut(["step-a"]).Check([candidate], Context());

        Assert.False(candidate.IncludedInExecutionPlan);
        Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
        var message = Assert.Single(candidate.Messages);
        Assert.Equal(ProcessorErrorCodes.StepNotAvailable, message.Code);
        Assert.Contains("Available: step-a", message.Message);
    }

    [Fact]
    public void Check_WhenAStepIsRequired_OnlyAdmitsTheRequiredStep()
    {
        var other = Candidate<StepA>("step-a");
        var required = Candidate<StepB>("step-b");

        CreateSut(["step-a", "step-b"]).Check([other, required], Context() with { RequiredStep = "step-b" });

        Assert.False(other.IncludedInExecutionPlan);
        Assert.Contains("requires 'step-b' next", Assert.Single(other.Messages).Message);
        Assert.True(required.IncludedInExecutionPlan);
    }

    [Fact]
    public void Check_OnlyGatesTheFirstAdmittedStep_LeavingTheRestToTheEvaluator()
    {
        var first = Candidate<StepA>("step-a");
        var chained = Candidate<StepB>("step-b");

        CreateSut(["step-a"]).Check([first, chained], Context());

        Assert.True(first.IncludedInExecutionPlan);
        Assert.True(chained.IncludedInExecutionPlan);
        Assert.Empty(chained.Messages);
    }

    [Fact]
    public void Check_IgnoresCandidatesNotInThePlan()
    {
        var excluded = StepCandidate.Invalid("step-b", ProcessorErrorCodes.UnknownStep, "unknown");
        var admitted = Candidate<StepA>("step-a");

        CreateSut(["step-a"]).Check([excluded, admitted], Context());

        Assert.Single(excluded.Messages);
        Assert.True(admitted.IncludedInExecutionPlan);
    }

    [Fact]
    public void Check_InformationStepWithValidAnswers_IsAdmitted()
    {
        var candidate = InformationCandidate("out-of-network");
        var validator = new Mock<IInformationValidator>();
        validator
            .Setup(x => x.ValidateResponse(PendingRequest, (IInformationStep)candidate.Step!))
            .Returns([]);

        CreateSut([], validator.Object).Check([candidate], AwaitingInformation());

        Assert.True(candidate.IncludedInExecutionPlan);
        validator.VerifyAll();
    }

    [Fact]
    public void Check_InformationStepWithInvalidAnswers_IsRejectedWithEveryProblem()
    {
        var candidate = InformationCandidate("out-of-network");
        var validator = new Mock<IInformationValidator>();
        validator
            .Setup(x => x.ValidateResponse(PendingRequest, It.IsAny<IInformationStep>()))
            .Returns(
            [
                StepProcessingMessage.Error(ProcessorErrorCodes.InformationResponseUnanswered, "Question 'q1' has no answer."),
                StepProcessingMessage.Error(ProcessorErrorCodes.InformationResponseInvalidAnswer, "Item 'x' is not part of it.")
            ]);

        CreateSut([], validator.Object).Check([candidate], AwaitingInformation());

        Assert.False(candidate.IncludedInExecutionPlan);
        Assert.Equal(
            [ProcessorErrorCodes.InformationResponseUnanswered, ProcessorErrorCodes.InformationResponseInvalidAnswer],
            candidate.Messages.Select(x => x.Code));
    }

    [Fact]
    public void Check_InformationStepWithoutPendingRequest_IsAMismatch()
    {
        var candidate = InformationCandidate("out-of-network");

        CreateSut([], Mock.Of<IInformationValidator>(MockBehavior.Strict))
            .Check([candidate], Context() with { RequiredStep = "info-step" });

        Assert.Equal(ProcessorErrorCodes.InformationResponseMismatch, Assert.Single(candidate.Messages).Code);
    }

    private static ProcessorContext Context() =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor"
        };

    private static ProcessorContext AwaitingInformation() =>
        Context() with
        {
            State = ProcessExecutionState.AwaitingInformation,
            RequiredStep = "info-step",
            RequiredInformationRequest = PendingRequest
        };

    private static StepCandidate Candidate<TStep>(string name)
        where TStep : IProcessStep, new() =>
        new()
        {
            StepName = name,
            Registration = Registration(typeof(TStep), name),
            Status = StepCandidateStatus.Built,
            IncludedInExecutionPlan = true,
            Step = new TStep()
        };

    private static StepCandidate InformationCandidate(string informationRequestId) =>
        new()
        {
            StepName = "info-step",
            Registration = Registration(typeof(InfoStep), "info-step"),
            Status = StepCandidateStatus.Built,
            IncludedInExecutionPlan = true,
            Step = new InfoStep { InformationRequestId = informationRequestId }
        };

    private static ProcessStepRegistration Registration(Type stepType, string name) =>
        new(
            stepType,
            null,
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions(),
            new ProcessStepMetadata(name, $"{name} description.", "1.0", name));

    private sealed class StepA : IProcessStep;

    private sealed class StepB : IProcessStep;

    private sealed class InfoStep : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
