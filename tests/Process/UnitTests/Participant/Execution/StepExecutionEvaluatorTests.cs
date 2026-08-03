using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.Tests.Participant.Execution;

public sealed class StepExecutionEvaluatorTests
{
    [Fact]
    public void Constructor_WhenRegistryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new StepExecutionEvaluator(null!));

        Assert.Equal("registry", exception.ParamName);
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
                    []));

        Assert.Equal("currentCandidate", exception.ParamName);
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
                    []));

        Assert.Equal("result", exception.ParamName);
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
                    null!));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public void Evaluate_WhenExecutionFailed_ReturnsBusinessFailure()
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
                []);

        Assert.Equal(
            ExecutionDecisionType.BusinessFailure,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsNotAllowed_ReturnsProcessViolation()
    {
        var evaluator =
            CreateEvaluator();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-c"
                },
                []);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            decision.Type);

        var message =
            Assert.Single(decision.Messages);

        Assert.Equal(
            StepProcessingMessageCode.RequiredStepNotAllowed,
            message.Code);
    }

    [Fact]
    public void Complete_InitializesEmptyMessagesCollection()
    {
        var decision =
            ExecutionDecision.Complete();

        Assert.NotNull(decision.Messages);
        Assert.Empty(decision.Messages);
    }

    [Fact]
    public void BusinessFailure_InitializesEmptyMessagesCollection()
    {
        var decision =
            ExecutionDecision.BusinessFailure();

        Assert.NotNull(decision.Messages);
        Assert.Empty(decision.Messages);
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

        Assert.Single(decision.Messages);

        Assert.Same(
            message,
            decision.Messages.Single());
    }

    [Fact]
    public void Evaluate_WhenRequiredStepAllowedButMissing_ReturnsAwaitingRequiredStep()
    {
        var stepB =
            CreateRegistration<StepB>("step-b");

        var evaluator =
            CreateEvaluator(
                (typeof(StepA), [stepB]));

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-b"
                },
                []);

        Assert.Equal(
            ExecutionDecisionType.AwaitingRequiredStep,
            decision.Type);

        Assert.Equal(
            "step-b",
            decision.RequiredStep);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepAllowedAndCandidateExists_ReturnsContinue()
    {
        var stepB =
            CreateRegistration<StepB>("step-b");

        var evaluator =
            CreateEvaluator(
                (typeof(StepA), [stepB]));

        var nextCandidate =
            CreateCandidate<StepB>("step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "step-b"
                },
                [nextCandidate]);

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
        var stepB =
            CreateRegistration<StepB>("step-b");

        var evaluator =
            CreateEvaluator(
                (typeof(StepA), [stepB]));

        var nextCandidate =
            CreateCandidate<StepB>("step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true
                },
                [nextCandidate]);

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);

        Assert.Same(
            nextCandidate,
            decision.NextCandidate);
    }

    [Fact]
    public void Evaluate_WhenNoCandidateExistsButAvailableStepsExist_ReturnsAwaitingStepSelection()
    {
        var stepB =
            CreateRegistration<StepB>("step-b");

        var stepC =
            CreateRegistration<StepC>("step-c");

        var evaluator =
            CreateEvaluator(
                (typeof(StepA), [stepB, stepC]));

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true
                },
                []);

        Assert.Equal(
            ExecutionDecisionType.AwaitingStepSelection,
            decision.Type);

        Assert.Contains("step-b", decision.AvailableSteps);
        Assert.Contains("step-c", decision.AvailableSteps);
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
                []);

        Assert.Equal(
            ExecutionDecisionType.Complete,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepUsesDifferentCasing_MatchesCaseInsensitively()
    {
        var stepB =
            CreateRegistration<StepB>("step-b");

        var evaluator =
            CreateEvaluator(
                (typeof(StepA), [stepB]));

        var nextCandidate =
            CreateCandidate<StepB>("STEP-B");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = "Step-B"
                },
                [nextCandidate]);

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);
    }

    private static StepExecutionEvaluator CreateEvaluator(
        params (Type StepType, ProcessStepRegistration[] Dependents)[] dependents)
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x => x.GetDependents(It.IsAny<Type>()))
            .Returns([]);

        foreach (var dependent in dependents)
        {
            registry
                .Setup(x =>
                    x.GetDependents(dependent.StepType))
                .Returns(dependent.Dependents);
        }

        return new StepExecutionEvaluator(
            registry.Object);
    }

    private static StepCandidate CreateCandidate<TStep>(
        string name)
    {
        return new StepCandidate
        {
            StepName = name,
            Registration =
                CreateRegistration<TStep>(name),
            Status = StepCandidateStatus.Built,
            Step = new object()
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(object),
            typeof(object),
            new ProcessStepMetadata(
                name,
                name,
                "1.0"));
    }

    private sealed class StepA;

    private sealed class StepB;

    private sealed class StepC;
}