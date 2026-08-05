using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant.Planning;

public sealed class StepCandidatePlannerTests
{
    [Fact]
    public void Build_WhenCandidatesIsNull_Throws()
    {
        var planner =
            CreatePlanner();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                planner.Build(null!));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public void Build_WhenNoCandidates_ReturnsEmptyCollection()
    {
        var planner =
            CreatePlanner();

        var result =
            planner.Build([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Build_WhenCandidateIsBuilt_IncludesInExecutionPlan()
    {
        var planner =
            CreatePlanner();

        var candidate =
            CreateCandidate<StepA>(
                StepCandidateStatus.Built);

        var result =
            planner.Build([candidate]);

        var planned =
            Assert.Single(result);

        Assert.True(planned.IncludedInExecutionPlan);
    }

    [Fact]
    public void Build_WhenCandidateIsInvalid_DoesNotIncludeInExecutionPlan()
    {
        var planner =
            CreatePlanner();

        var candidate =
            CreateCandidate<StepA>(
                StepCandidateStatus.Invalid);

        var result =
            planner.Build([candidate]);

        var returned =
            Assert.Single(result);

        Assert.False(returned.IncludedInExecutionPlan);
    }

    [Fact]
    public void Build_WhenCandidateIsSatisfied_DoesNotIncludeInExecutionPlan()
    {
        var planner =
            CreatePlanner();

        var candidate =
            CreateCandidate<StepA>(
                StepCandidateStatus.Satisfied);

        var result =
            planner.Build([candidate]);

        var returned =
            Assert.Single(result);

        Assert.False(returned.IncludedInExecutionPlan);
    }

    [Fact]
    public void Build_WhenMultipleBuiltCandidates_AllIncludedInExecutionPlan()
    {
        var planner =
            CreatePlanner();

        var candidateA =
            CreateCandidate<StepA>(
                StepCandidateStatus.Built);

        var candidateB =
            CreateCandidate<StepB>(
                StepCandidateStatus.Built);

        var result =
            planner.Build(
            [
                candidateA,
                candidateB
            ]);

        Assert.All(
            result,
            candidate =>
                Assert.True(candidate.IncludedInExecutionPlan));
    }

    [Fact]
    public void Build_WhenMixedStatuses_OnlyBuiltCandidatesIncludedInExecutionPlan()
    {
        var planner =
            CreatePlanner();

        var built =
            CreateCandidate<StepA>(
                StepCandidateStatus.Built);

        var invalid =
            CreateCandidate<StepB>(
                StepCandidateStatus.Invalid);

        var satisfied =
            CreateCandidate<StepC>(
                StepCandidateStatus.Satisfied);

        var result =
            planner.Build(
            [
                built,
                invalid,
                satisfied
            ]);

        Assert.True(built.IncludedInExecutionPlan);
        Assert.False(invalid.IncludedInExecutionPlan);
        Assert.False(satisfied.IncludedInExecutionPlan);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Build_WhenDependencyExists_OrdersDependencyBeforeDependent()
    {
        var registrationA =
            CreateRegistration<StepA>("step-a");

        var planner =
            CreatePlanner();

        var stepB =
            CreateCandidate<StepB>(
                StepCandidateStatus.Built,
                [registrationA]);

        var stepA =
            CreateCandidate<StepA>(
                StepCandidateStatus.Built);

        var result =
            planner.Build(
            [
                stepB,
            stepA
            ]);

        Assert.Equal(
            typeof(StepA),
            result.ElementAt(0).Registration!.StepType);

        Assert.Equal(
            typeof(StepB),
            result.ElementAt(1).Registration!.StepType);
    }

    [Fact]
    public void Build_WhenMultipleDependencyLevels_OrdersEntireDependencyChain()
    {
        var registrationA =
            CreateRegistration<StepA>("step-a");

        var registrationB =
            CreateRegistration<StepB>(
                "step-b",
                [registrationA]);

        var registrationC =
            CreateRegistration<StepC>(
                "step-c",
                [registrationB]);

        var planner =
            CreatePlanner();

        var stepC =
            CreateCandidate(
                StepCandidateStatus.Built,
                registrationC);

        var stepA =
            CreateCandidate(
                StepCandidateStatus.Built,
                registrationA);

        var stepB =
            CreateCandidate(
                StepCandidateStatus.Built,
                registrationB);

        var result =
            planner.Build(
            [
                stepC,
            stepA,
            stepB
            ]);

        Assert.Equal(
            typeof(StepA),
            result.ElementAt(0).Registration!.StepType);

        Assert.Equal(
            typeof(StepB),
            result.ElementAt(1).Registration!.StepType);

        Assert.Equal(
            typeof(StepC),
            result.ElementAt(2).Registration!.StepType);
    }

    [Fact]
    public void Build_WhenDependencyNotInCandidateList_IgnoresMissingDependency()
    {
        var dependency =
            CreateRegistration<StepA>("step-a");

        var planner =
            CreatePlanner(
                (typeof(StepB), [dependency]));

        var stepB =
            CreateCandidate<StepB>(
                StepCandidateStatus.Built);

        var result =
            planner.Build([stepB]);

        var candidate =
            Assert.Single(result);

        Assert.Same(stepB, candidate);
        Assert.True(candidate.IncludedInExecutionPlan);
    }

    [Fact]
    public void Build_WhenNonExecutableCandidatesExist_ReturnsThemAfterPlannedCandidates()
    {
        var planner =
            CreatePlanner();

        var built =
            CreateCandidate<StepA>(
                StepCandidateStatus.Built);

        var invalid =
            CreateCandidate<StepB>(
                StepCandidateStatus.Invalid);

        var satisfied =
            CreateCandidate<StepC>(
                StepCandidateStatus.Satisfied);

        var result =
            planner.Build(
            [
                built,
                invalid,
                satisfied
            ]);

        Assert.Equal(
            StepCandidateStatus.Built,
            result.ElementAt(0).Status);

        Assert.Contains(
            result,
            x => x.Status == StepCandidateStatus.Invalid);

        Assert.Contains(
            result,
            x => x.Status == StepCandidateStatus.Satisfied);
    }

    private static StepCandidatePlanner CreatePlanner(
        params (Type StepType, ProcessStepRegistration[] Dependencies)[] dependencies)
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        return new StepCandidatePlanner();
    }

    private static StepCandidate CreateCandidate<TStep>(
        StepCandidateStatus status,
        IReadOnlyCollection<ProcessStepRegistration>? dependencies = null)
    {
        return new StepCandidate
        {
            StepName = typeof(TStep).Name,
            Status = status,
            Step = new object(),

            Registration =
                new ProcessStepRegistration(
                    typeof(TStep),
                    typeof(object),
                    typeof(object),
                    dependencies ?? [],
                    [],
                    [],
                    new ProcessStepMetadata(
                        typeof(TStep).Name,
                        $"{typeof(TStep).Name} description.",
                        "1.0"))
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name,
        IReadOnlyCollection<ProcessStepRegistration>? dependencies = null)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(object),
            typeof(object),
            dependencies ?? [],
            [],
            [],
            new ProcessStepMetadata(
                name,
                $"{name} description.",
                "1.0"));
    }

    private static StepCandidate CreateCandidate(
        StepCandidateStatus status,
        ProcessStepRegistration registration)
    {
        return new StepCandidate
        {
            StepName = registration.StepType.Name,
            Status = status,
            Step = new object(),
            Registration = registration
        };
    }

    private sealed class StepA;

    private sealed class StepB;

    private sealed class StepC;
}