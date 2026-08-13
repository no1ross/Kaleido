using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant.Execution;

public sealed class StepExecutionEvaluatorTests
{
    [Fact]
    public void Constructor_WhenAvailabilityResolverIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new StepExecutionEvaluator(
                    null!));

        Assert.Equal(
            "availabilityResolver",
            exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenCandidateIsNull_Throws()
    {
        var evaluator =
            CreateEvaluator();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                evaluator.Evaluate(
                    null!,
                    new ProcessStepInvokerResult(),
                    [],
                    CreateContext()));

        Assert.Equal(
            "currentCandidate",
            exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenResultIsNull_Throws()
    {
        var evaluator =
            CreateEvaluator();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                evaluator.Evaluate(
                    CreateCandidate<StepA>("step-a"),
                    null!,
                    [],
                    CreateContext()));

        Assert.Equal(
            "result",
            exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenCandidatesIsNull_Throws()
    {
        var evaluator =
            CreateEvaluator();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                evaluator.Evaluate(
                    CreateCandidate<StepA>("step-a"),
                    new ProcessStepInvokerResult(),
                    null!,
                    CreateContext()));

        Assert.Equal(
            "candidates",
            exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenContextIsNull_Throws()
    {
        var evaluator =
            CreateEvaluator();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                evaluator.Evaluate(
                    CreateCandidate<StepA>("step-a"),
                    new ProcessStepInvokerResult(),
                    [],
                    null!));

        Assert.Equal(
            "context",
            exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenExecutionFails_ReturnsBusinessFailure()
    {
        var evaluator =
            CreateEvaluator();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = false
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.BusinessFailure,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsNotAvailable_ReturnsProcessViolation()
    {
        var evaluator =
            CreateEvaluator(
                ["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-c"
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            decision.Type);

        var message =
            Assert.Single(
                decision.Messages);

        Assert.Equal(
            StepProcessingMessageCode.RequiredStepNotAllowed,
            message.Code);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsAvailableButNotSupplied_ReturnsAwaitingRequiredStep()
    {
        var evaluator =
            CreateEvaluator(
                ["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-b"
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingRequiredStep,
            decision.Type);

        Assert.Equal(
            "step-b",
            decision.RequiredStep);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsAvailableAndCandidateExists_ReturnsContinue()
    {
        var evaluator =
            CreateEvaluator(
                ["step-b"]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-b"
                },
                [nextCandidate],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);

        Assert.Same(
            nextCandidate,
            decision.NextCandidate);
    }

    [Fact]
    public void Evaluate_WhenAvailableCandidateExists_ReturnsContinue()
    {
        var evaluator =
            CreateEvaluator(
                ["step-b"]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true
                },
                [nextCandidate],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);

        Assert.Same(
            nextCandidate,
            decision.NextCandidate);
    }

    [Fact]
    public void Evaluate_WhenAvailableStepsExistButCandidateDoesNotExist_ReturnsAwaitingStepSelection()
    {
        var evaluator =
            CreateEvaluator(
                [
                    "step-b",
                    "step-c"
                ]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingStepSelection,
            decision.Type);

        Assert.Contains(
            "step-b",
            decision.AvailableSteps);

        Assert.Contains(
            "step-c",
            decision.AvailableSteps);
    }

    [Fact]
    public void Evaluate_WhenNoAvailableStepsExist_ReturnsComplete()
    {
        var evaluator =
            CreateEvaluator();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Complete,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepUsesDifferentCasing_MatchesCaseInsensitively()
    {
        var evaluator =
            CreateEvaluator(
                ["step-b"]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "STEP-B");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "Step-B"
                },
                [nextCandidate],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);
    }

    [Fact]
    public void Complete_InitializesEmptyMessagesCollection()
    {
        var decision =
            ExecutionDecision.Complete();

        Assert.NotNull(
            decision.Messages);

        Assert.Empty(
            decision.Messages);
    }

    [Fact]
    public void BusinessFailure_InitializesEmptyMessagesCollection()
    {
        var decision =
            ExecutionDecision.BusinessFailure();

        Assert.NotNull(
            decision.Messages);

        Assert.Empty(
            decision.Messages);
    }

    [Fact]
    public void ProcessViolation_SetsSingleMessage()
    {
        var message =
            StepProcessingMessage.Error(
                StepProcessingMessageCode.RequiredStepNotAllowed,
                "test");

        var decision =
            ExecutionDecision.ProcessViolation(
                message);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            decision.Type);

        Assert.Single(
            decision.Messages);

        Assert.Same(
            message,
            decision.Messages.Single());
    }

    private static StepExecutionEvaluator CreateEvaluator(
        IReadOnlyCollection<string>? availableSteps = null)
    {
        var resolver =
            new Mock<IStepAvailabilityResolver>();

        resolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ParticipantContext>()))
            .Returns(
                availableSteps ?? []);

        return new StepExecutionEvaluator(
            resolver.Object);
    }

    private static ParticipantContext CreateContext()
    {
        return new ParticipantContext()
        {
            ParticipantProcessId = Guid.NewGuid()
        };
    }

    private static StepCandidate CreateCandidate<TStep>(
        string name)
    {
        return new StepCandidate
        {
            StepName =
                name,

            Registration =
                CreateRegistration<TStep>(
                    name),

            Status =
                StepCandidateStatus.Built,

            Step =
                Activator.CreateInstance(
                    typeof(TStep))!
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions(),
            new ProcessStepMetadata(
                name,
                $"{name} description.",
                "1.0",
                $"{name} displayname"));
    }

    private sealed class StepA;

    private sealed class StepB;

    private sealed class StepC;
}