using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Execution.UnitTests;

public sealed class StepExecutionEvaluatorTests
    : SutFixture
{
    private const string LocalProcessorName = "test-processor";

    [Fact]
    public void Evaluate_WhenExecutionFails_ReturnsBusinessFailure()
    {
        var evaluator =
            CreateSut();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
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
            CreateSut(
                ["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepC)
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
            ProcessorErrorCodes.RequiredStepNotAllowed,
            message.Code);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsAvailableButNotSupplied_ReturnsAwaitingRequiredStep()
    {
        var evaluator =
            CreateSut(["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepB)
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingRequiredStep,
            decision.Type);

        Assert.Equal(
            "step-b",
            decision.RequiredStep);

        Assert.Null(
            decision.TargetProcessorName);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsAvailableAndCandidateExists_ReturnsContinue()
    {
        var evaluator =
            CreateSut(
                ["step-b"]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepB)
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
            CreateSut(
                ["step-b"]);

        var nextCandidate =
            CreateCandidate<StepB>(
                "step-b");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
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
            CreateSut(
                ["step-b", "step-c"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.AwaitingStepSelection,
            decision.Type);

        Assert.Contains(decision.AvailableSteps, x => x == "step-b");

        Assert.Contains(decision.AvailableSteps, x => x == "step-c");
    }

    [Fact]
    public void Evaluate_WhenNoAvailableStepsExist_ReturnsComplete()
    {
        var evaluator =
            CreateSut();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
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
            CreateSut(
                ["step-b"],
                new Dictionary<Type, string> { [typeof(StepB)] = "Step-B" });

        var nextCandidate =
            CreateCandidate<StepB>(
                "STEP-B");

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepB)
                },
                [nextCandidate],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.Continue,
            decision.Type);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepTypeIsNotRegistered_ReturnsProcessViolation()
    {
        var evaluator =
            CreateSut(
                ["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(UnregisteredStep)
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
            ProcessorErrorCodes.RequiredStepNotAllowed,
            message.Code);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsExternalProcessor_ReturnsHandOff()
    {
        // RequiredStep + TargetProcessorName pointing to an external processor
        // means the step succeeded and execution must continue on the target processor.
        // The evaluator should return HandOff, not AwaitingRequiredStep.
        var evaluator =
            CreateSut(
                ["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepC),
                    TargetProcessorName = "radiology"
                },
                [],
                CreateContext());

        Assert.Equal(
            ExecutionDecisionType.HandOff,
            decision.Type);

        Assert.Equal(
            "radiology",
            decision.TargetProcessorName);
    }

    [Fact]
    public void Evaluate_WhenRequiredStepIsExternalProcessor_DoesNotReturnProcessViolation()
    {
        // Even though the external step is not in local available steps,
        // it must NOT be treated as a process violation.
        var evaluator =
            CreateSut();

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult
                {
                    Succeeded = true,
                    RequiredStep = typeof(StepC),
                    TargetProcessorName = "radiology"
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
                ProcessorErrorCodes.RequiredStepNotAllowed,
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

    private static readonly InformationRequest Request =
        new()
        {
            InformationRequestId = "out-of-network",
            Items = [new InformationItem { Id = "q1", Text = "Proceed?", Type = InformationItemType.Boolean }]
        };

    [Fact]
    public void Evaluate_WhenInformationStepRequiredWithRequest_ReturnsAwaitingInformation()
    {
        var evaluator = CreateSut(["info-step"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true, RequiredStep = typeof(InfoStep), InformationRequest = Request },
                [],
                CreateContext());

        Assert.Equal(ExecutionDecisionType.AwaitingInformation, decision.Type);
        Assert.Equal("info-step", decision.RequiredStep);
        Assert.Same(Request, decision.InformationRequest);
    }

    [Fact]
    public void Evaluate_WhenInformationStepRequired_NeverContinuesIntoASubmittedCandidate()
    {
        var evaluator = CreateSut(["info-step"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true, RequiredStep = typeof(InfoStep), InformationRequest = Request },
                [CreateInfoCandidate()],
                CreateContext());

        Assert.Equal(ExecutionDecisionType.AwaitingInformation, decision.Type);
        Assert.Null(decision.NextCandidate);
    }

    [Fact]
    public void Evaluate_WhenInformationStepRequiredWithoutRequest_ReturnsProcessViolation()
    {
        var evaluator = CreateSut(["info-step"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true, RequiredStep = typeof(InfoStep) },
                [],
                CreateContext());

        Assert.Equal(ExecutionDecisionType.ProcessViolation, decision.Type);
        Assert.Equal(ProcessorErrorCodes.InformationRequestMissing, Assert.Single(decision.Messages).Code);
    }

    [Fact]
    public void Evaluate_WhenInformationRequestIsNotWellFormed_ReturnsProcessViolation()
    {
        var validator = new Mock<IInformationValidator>();
        validator
            .Setup(x => x.ValidateRequest(Request))
            .Returns([StepProcessingMessage.Error(ProcessorErrorCodes.InformationRequestInvalid, "Item 'q1' has no text.")]);

        var evaluator = CreateSut(["info-step"], informationValidator: validator.Object);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true, RequiredStep = typeof(InfoStep), InformationRequest = Request },
                [],
                CreateContext());

        var message = Assert.Single(decision.Messages);
        Assert.Equal(ExecutionDecisionType.ProcessViolation, decision.Type);
        Assert.Equal(ProcessorErrorCodes.InformationRequestInvalid, message.Code);
        Assert.Contains("Item 'q1' has no text.", message.Message);
    }

    [Fact]
    public void Evaluate_WhenInformationStepIsNotAValidNextStep_ReturnsProcessViolation()
    {
        var evaluator = CreateSut(["step-b"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true, RequiredStep = typeof(InfoStep), InformationRequest = Request },
                [],
                CreateContext());

        Assert.Equal(ProcessorErrorCodes.RequiredStepNotAllowed, Assert.Single(decision.Messages).Code);
    }

    [Fact]
    public void Evaluate_WhenNothingRequired_DoesNotContinueIntoAnInformationStep()
    {
        var evaluator = CreateSut(["step-b", "info-step"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true },
                [CreateInfoCandidate()],
                CreateContext());

        Assert.Equal(ExecutionDecisionType.AwaitingStepSelection, decision.Type);
    }

    [Fact]
    public void Evaluate_WhenNothingRequired_NeverOffersAnInformationStep()
    {
        var evaluator = CreateSut(["step-b", "info-step"]);

        var decision =
            evaluator.Evaluate(
                CreateCandidate<StepA>("step-a"),
                new StepInvocationResult { Succeeded = true },
                [],
                CreateContext());

        Assert.Equal(ExecutionDecisionType.AwaitingStepSelection, decision.Type);
        Assert.Equal(["step-b"], decision.AvailableSteps);
    }

    private static StepCandidate CreateInfoCandidate() =>
        new()
        {
            StepName = "info-step",
            Registration = CreateRegistration<InfoStep>("info-step"),
            Status = StepCandidateStatus.Built,
            Step = new InfoStep { InformationRequestId = "out-of-network" }
        };

    private static StepExecutionEvaluator CreateSut(
        IReadOnlyCollection<string>? availableSteps = null,
        IReadOnlyDictionary<Type, string>? registeredNames = null,
        IInformationValidator? informationValidator = null)
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

        var names =
            registeredNames ?? new Dictionary<Type, string>
            {
                [typeof(StepA)] = "step-a",
                [typeof(StepB)] = "step-b",
                [typeof(StepC)] = "step-c",
                [typeof(InfoStep)] = "info-step"
            };

        var registry =
            new Mock<IProcessorStepRegistry>();

        registry
            .Setup(x => x.Find(It.IsAny<Type>()))
            .Returns((Type type) =>
                names.TryGetValue(type, out var name)
                    ? CreateRegistration(type, name)
                    : null);

        registry
            .Setup(x => x.Find(It.IsAny<string>()))
            .Returns((string stepName) =>
                names.FirstOrDefault(x => string.Equals(x.Value, stepName, StringComparison.OrdinalIgnoreCase)) is { Key: { } type, Value: { } name }
                    ? CreateRegistration(type, name)
                    : null);

        return new StepExecutionEvaluator(
            resolver.Object,
            registry.Object,
            informationValidator ?? Mock.Of<IInformationValidator>(x => x.ValidateRequest(It.IsAny<InformationRequest>()) == Array.Empty<StepProcessingMessage>()),
            new KaleidoServiceOptions { ServiceName = LocalProcessorName });
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
                Activator.CreateInstance<TStep>()!
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name) =>
        CreateRegistration(typeof(TStep), name);

    private static ProcessStepRegistration CreateRegistration(
        Type stepType,
        string name)
    {
        return new ProcessStepRegistration(
            stepType,
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

    private sealed class StepA : IProcessStep;

    private sealed class StepB : IProcessStep;

    private sealed class StepC : IProcessStep;

    private sealed class UnregisteredStep : IProcessStep;

    private sealed class InfoStep : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
