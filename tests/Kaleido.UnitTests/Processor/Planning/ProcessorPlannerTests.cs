using Kaleido.Processor.Context;
using Kaleido.Processor.Planning;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Planning.UnitTests;

public sealed class ProcessorPlannerTests
    : SutFixture
{
    private static ProcessorPlanner CreateSut(
        IStepCandidateBuilder candidateBuilder,
        IStepCandidateValidator validator,
        IStepCandidateConsistencyChecker consistencyChecker,
        IStepCandidatePlanner candidatePlanner,
        IStepCandidateNextStepChecker? nextStepChecker = null) =>
        new(candidateBuilder, validator, consistencyChecker, candidatePlanner, nextStepChecker ?? Mock.Of<IStepCandidateNextStepChecker>());

    [Fact]
    public void BuildPlan_CallsCollaboratorsInOrder()
    {
        var request =
            new ProcessorRequest();

        var context =
            new ProcessorContext
            {
                ProcessId = Guid.NewGuid(),
                ProcessorName = "test-processor"
            };

        var candidates =
            new[]
            {
                new StepCandidate{ StepName = "Test Step" }
            };

        var orderedCandidates =
            new[]
            {
                new StepCandidate{ StepName = "Ordered Step 1" },
                new StepCandidate{ StepName = "Ordered Step 2" }
            };

        var sequence =
            new MockSequence();

        var candidateBuilder =
            new Mock<IStepCandidateBuilder>(MockBehavior.Strict);

        candidateBuilder
            .InSequence(sequence)
            .Setup(x => x.Build(request))
            .Returns(candidates);

        var validator =
            new Mock<IStepCandidateValidator>(MockBehavior.Strict);

        validator
            .InSequence(sequence)
            .Setup(x => x.Validate(candidates));

        var consistencyChecker =
            new Mock<IStepCandidateConsistencyChecker>(MockBehavior.Strict);

        consistencyChecker
            .InSequence(sequence)
            .Setup(x =>
                x.Validate(
                    candidates,
                    context));

        var candidatePlanner =
            new Mock<IStepCandidatePlanner>(MockBehavior.Strict);

        candidatePlanner
            .InSequence(sequence)
            .Setup(x => x.Build(candidates))
            .Returns(orderedCandidates);

        var nextStepChecker =
            new Mock<IStepCandidateNextStepChecker>(MockBehavior.Strict);

        nextStepChecker
            .InSequence(sequence)
            .Setup(x => x.Check(orderedCandidates, context));

        var planner =
            CreateSut(
                candidateBuilder.Object,
                validator.Object,
                consistencyChecker.Object,
                candidatePlanner.Object,
                nextStepChecker.Object);

        planner.BuildPlan(
            request,
            context);

        candidateBuilder.VerifyAll();
        validator.VerifyAll();
        consistencyChecker.VerifyAll();
        candidatePlanner.VerifyAll();
        nextStepChecker.VerifyAll();
    }

    [Fact]
    public void BuildPlan_ReturnsOrderedCandidatesFromPlanner()
    {
        var request =
            new ProcessorRequest();

        var context =
            new ProcessorContext
            {
                ProcessId = Guid.NewGuid(),
                ProcessorName = "test-processor"
            };

        var candidates =
            new[]
            {
                new StepCandidate{ StepName = "Test Step" }
            };

        var orderedCandidates =
            new[]
            {
                new StepCandidate{ StepName = "Ordered Step 1" },
                new StepCandidate{ StepName = "Ordered Step 2" }
            };

        var candidateBuilder =
            new Mock<IStepCandidateBuilder>();

        candidateBuilder
            .Setup(x => x.Build(request))
            .Returns(candidates);

        var validator =
            new Mock<IStepCandidateValidator>();

        var consistencyChecker =
            new Mock<IStepCandidateConsistencyChecker>();

        var candidatePlanner =
            new Mock<IStepCandidatePlanner>();

        candidatePlanner
            .Setup(x => x.Build(candidates))
            .Returns(orderedCandidates);

        var planner =
            CreateSut(
                candidateBuilder.Object,
                validator.Object,
                consistencyChecker.Object,
                candidatePlanner.Object);

        var result =
            planner.BuildPlan(
                request,
                context);

        Assert.NotNull(result);
        Assert.Same(
            orderedCandidates,
            result.Candidates);
    }

    [Fact]
    public void BuildPlan_WhenValidatorThrows_StopsProcessing()
    {
        var request =
            new ProcessorRequest();

        var context =
            new ProcessorContext
            {
                ProcessId = Guid.NewGuid(),
                ProcessorName = "test-processor"
            };

        var candidates =
            new[]
            {
                new StepCandidate{ StepName = "Test Step" }
            };

        var candidateBuilder =
            new Mock<IStepCandidateBuilder>();

        candidateBuilder
            .Setup(x => x.Build(request))
            .Returns(candidates);

        var validator =
            new Mock<IStepCandidateValidator>();

        validator
            .Setup(x => x.Validate(candidates))
            .Throws<InvalidOperationException>();

        var consistencyChecker =
            new Mock<IStepCandidateConsistencyChecker>(MockBehavior.Strict);

        var candidatePlanner =
            new Mock<IStepCandidatePlanner>(MockBehavior.Strict);

        var planner =
            CreateSut(
                candidateBuilder.Object,
                validator.Object,
                consistencyChecker.Object,
                candidatePlanner.Object);

        Assert.Throws<InvalidOperationException>(() =>
            planner.BuildPlan(
                request,
                context));

        consistencyChecker.Verify(
            x => x.Validate(
                It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                It.IsAny<ProcessorContext>()),
            Times.Never);

        candidatePlanner.Verify(
            x => x.Build(
                It.IsAny<IReadOnlyCollection<StepCandidate>>()),
            Times.Never);
    }

    [Fact]
    public void BuildPlan_WhenConsistencyCheckerThrows_StopsProcessing()
    {
        var request =
            new ProcessorRequest();

        var context =
            new ProcessorContext
            {
                ProcessId = Guid.NewGuid(),
                ProcessorName = "test-processor"
            };

        var candidates =
            new[]
            {
                new StepCandidate{ StepName = "Test Step" }
            };

        var candidateBuilder =
            new Mock<IStepCandidateBuilder>();

        candidateBuilder
            .Setup(x => x.Build(request))
            .Returns(candidates);

        var validator =
            new Mock<IStepCandidateValidator>();

        var consistencyChecker =
            new Mock<IStepCandidateConsistencyChecker>();

        consistencyChecker
            .Setup(x =>
                x.Validate(
                    candidates,
                    context))
            .Throws<InvalidOperationException>();

        var candidatePlanner =
            new Mock<IStepCandidatePlanner>(MockBehavior.Strict);

        var planner =
            CreateSut(
                candidateBuilder.Object,
                validator.Object,
                consistencyChecker.Object,
                candidatePlanner.Object);

        Assert.Throws<InvalidOperationException>(() =>
            planner.BuildPlan(
                request,
                context));

        candidatePlanner.Verify(
            x => x.Build(
                It.IsAny<IReadOnlyCollection<StepCandidate>>()),
            Times.Never);
    }
}
