using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;
using Moq;
using Xunit;

namespace Kaleido.Process.Tests.Participant.Planning;

public sealed class ExecutionPlannerTests
{
    [Fact]
    public void Constructor_WhenCandidateBuilderIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionPlanner(
                    null!,
                    Mock.Of<IStepCandidateValidator>(),
                    Mock.Of<IStepCandidateConsistencyChecker>(),
                    Mock.Of<IStepCandidatePlanner>()));

        Assert.Equal("candidateBuilder", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenCandidateValidatorIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionPlanner(
                    Mock.Of<IStepCandidateBuilder>(),
                    null!,
                    Mock.Of<IStepCandidateConsistencyChecker>(),
                    Mock.Of<IStepCandidatePlanner>()));

        Assert.Equal("candidateValidator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenCandidateConsistencyCheckerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionPlanner(
                    Mock.Of<IStepCandidateBuilder>(),
                    Mock.Of<IStepCandidateValidator>(),
                    null!,
                    Mock.Of<IStepCandidatePlanner>()));

        Assert.Equal("candidateConsistencyChecker", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStepCandidatePlannerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionPlanner(
                    Mock.Of<IStepCandidateBuilder>(),
                    Mock.Of<IStepCandidateValidator>(),
                    Mock.Of<IStepCandidateConsistencyChecker>(),
                    null!));

        Assert.Equal("stepCandidatePlanner", exception.ParamName);
    }

    [Fact]
    public void BuildPlan_WhenRequestIsNull_Throws()
    {
        var planner =
            CreatePlanner();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                planner.BuildPlan(
                    null!,
                    new ParticipantContext()));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void BuildPlan_WhenContextIsNull_Throws()
    {
        var planner =
            CreatePlanner();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                planner.BuildPlan(
                    new ParticipantRequest(),
                    null!));

        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public void BuildPlan_CallsCollaboratorsInOrder()
    {
        var request =
            new ParticipantRequest();

        var context =
            new ParticipantContext();

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

        var planner =
            new ExecutionPlanner(
                candidateBuilder.Object,
                validator.Object,
                consistencyChecker.Object,
                candidatePlanner.Object);

        planner.BuildPlan(
            request,
            context);

        candidateBuilder.VerifyAll();
        validator.VerifyAll();
        consistencyChecker.VerifyAll();
        candidatePlanner.VerifyAll();
    }

    [Fact]
    public void BuildPlan_ReturnsOrderedCandidatesFromPlanner()
    {
        var request =
            new ParticipantRequest();

        var context =
            new ParticipantContext();

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
            new ExecutionPlanner(
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
            new ParticipantRequest();

        var context =
            new ParticipantContext();

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
            new ExecutionPlanner(
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
                It.IsAny<ParticipantContext>()),
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
            new ParticipantRequest();

        var context =
            new ParticipantContext();

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
            new ExecutionPlanner(
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

    private static ExecutionPlanner CreatePlanner()
    {
        return new ExecutionPlanner(
            Mock.Of<IStepCandidateBuilder>(),
            Mock.Of<IStepCandidateValidator>(),
            Mock.Of<IStepCandidateConsistencyChecker>(),
            Mock.Of<IStepCandidatePlanner>());
    }
}