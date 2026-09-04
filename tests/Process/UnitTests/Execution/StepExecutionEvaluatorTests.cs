using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Processor.Execution;

public sealed class StepExecutionEvaluatorTests
{
    private const string LocalProcessorName = "test-processor";

    [Fact]
    public void Constructor_WhenAvailabilityResolverIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new StepExecutionEvaluator(
                    null!,
                    CreateProcessorRegistry()));

        Assert.Equal(
            "availabilityResolver",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenProcessorRegistryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new StepExecutionEvaluator(
                    new Mock<IStepAvailabilityResolver>().Object,
                    null!));

        Assert.Equal(
            "processorRegistry",
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
                [CreateLocalReference("step-b")]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = CreateLocalReference("step-c")
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
                [CreateLocalReference("step-b")]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = CreateLocalReference("step-b")
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingRequiredStep,
            decision.Type);

        Assert.Equal(
            "step-b",
            decision.RequiredStep!.StepName);

        Assert.Equal(
            LocalProcessorName,
            decision.RequiredStep.ProcessorName);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsAvailableAndCandidateExists_ReturnsContinue()
    {
        var evaluator =
            CreateEvaluator(
                [CreateLocalReference("step-b")]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = CreateLocalReference("step-b")
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
                [CreateLocalReference("step-b")]);

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
                    CreateLocalReference("step-b"),
                    CreateLocalReference("step-c")
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
            decision.AvailableSteps,
            x => x.StepName == "step-b");

        Assert.Contains(
            decision.AvailableSteps,
            x => x.StepName == "step-c");
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
                [CreateLocalReference("step-b")]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "STEP-B");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = CreateLocalReference("Step-B")
                },
                [nextCandidate],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsExternalProcessor_SkipsLocalValidationAndReturnsAwaitingRequiredStep()
    {
        // External processor step — not in local available steps at all.
        // Should bypass local validation and return AwaitingRequiredStep directly.
        var evaluator =
            CreateEvaluator(
                [CreateLocalReference("step-b")]);

        var externalReference =
            new ProcessStepReference
            {
                ProcessorName = "radiology",
                StepName = "imaging-request"
            };

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = externalReference
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingRequiredStep,
            decision.Type);

        Assert.Equal(
            "radiology",
            decision.RequiredStep!.ProcessorName);

        Assert.Equal(
            "imaging-request",
            decision.RequiredStep.StepName);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsExternalProcessor_DoesNotReturnProcessViolation()
    {
        // Even though the external step is not in local available steps,
        // it must NOT be treated as a process violation.
        var evaluator =
            CreateEvaluator();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new ProcessStepInvokerResult
                {
                    Succeeded = true,
                    RequiredStep = new ProcessStepReference
                    {
                        ProcessorName = "radiology",
                        StepName = "imaging-request"
                    }
                },
                [],
                CreateContext());

        Assert.NotEqual(
            ExecutionDecisionType.ProcessViolation,
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

    private static ProcessStepReference CreateLocalReference(string stepName)
        => new()
        {
            ProcessorName = LocalProcessorName,
            StepName = stepName
        };

    private static StepExecutionEvaluator CreateEvaluator(
        IReadOnlyCollection<ProcessStepReference>? availableSteps = null)
    {
        var resolver =
            new Mock<IStepAvailabilityResolver>();

        resolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>()))
            .Returns(
                availableSteps ?? []);

        return new StepExecutionEvaluator(
            resolver.Object,
            CreateProcessorRegistry());
    }

    private static IProcessorRegistry CreateProcessorRegistry()
    {
        var item = new ProcessorRegistryItem
        {
            Name = LocalProcessorName,
            Description = "test",
            DisplayName = "Test Processor",
            Version = "1.0"
        };

        var mock = new Mock<IProcessorRegistry>();

        mock.Setup(x => x.Registrations)
            .Returns([item]);

        return mock.Object;
    }

    private static ProcessorContext CreateContext()
    {
        return new ProcessorContext()
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor"
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
