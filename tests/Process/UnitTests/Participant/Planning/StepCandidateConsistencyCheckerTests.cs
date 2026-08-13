using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant.Planning;

public sealed class StepCandidateConsistencyCheckerTests
{
    [Fact]
    public void Validate_WhenCandidatesIsNull_Throws()
    {
        var checker =
            CreateChecker();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                checker.Validate(
                    null!,
                    new ParticipantContext
                    {
                        ParticipantProcessId = Guid.NewGuid()
                    }));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenContextIsNull_Throws()
    {
        var checker =
            CreateChecker();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                checker.Validate(
                    [],
                    null!));

        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenCandidateAlreadyInvalid_SkipsValidation()
    {
        var checker =
            CreateChecker();

        var candidate =
            StepCandidate.Invalid(
                "step-a",
                StepProcessingMessageCode.InvalidRequest,
                "already invalid");

        checker.Validate(
            [candidate],
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid()
            });

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.Single(candidate.Messages);
    }

    [Fact]
    public void Validate_WhenStepWasPreviouslyCompleted_MarksCandidateSatisfied()
    {
        var checker =
            CreateChecker();

        var registration =
            CreateRegistration<StepA>("step-a");

        var candidate =
            CreateCandidate(
                registration);

        var context =
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid(),
                Steps =
                [
                    new StepContext
                    {
                        StepName = "step-a",
                        Status = StepExecutionStatus.Completed
                    }
                ]
            };

        checker.Validate(
            [candidate],
            context);

        Assert.Equal(
            StepCandidateStatus.Satisfied,
            candidate.Status);
    }

    [Fact]
    public void Validate_WhenHistoricalStepIsNotCompleted_DoesNotMarkSatisfied()
    {
        var checker =
            CreateChecker();

        var registration =
            CreateRegistration<StepA>("step-a");

        var candidate =
            CreateCandidate(registration);

        var context =
            new ParticipantContext
            {
                ParticipantProcessId =  Guid.NewGuid(),
                Steps =
                [
                    new StepContext
                    {
                        StepName = "step-a",
                        Status = StepExecutionStatus.Pending
                    }
                ]
            };

        checker.Validate(
            [candidate],
            context);

        Assert.NotEqual(
            StepCandidateStatus.Satisfied,
            candidate.Status);
    }

    [Fact]
    public void Validate_WhenDependencySatisfiedByHistory_RemainsValid()
    {
        var dependency =
            CreateRegistration<StepA>("step-a");

        var target =
            CreateRegistration<StepB>("step-b");

        var checker =
            CreateChecker(
                (typeof(StepB), [dependency]));

        var candidate =
            CreateCandidate(target);

        var context =
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid(),
                Steps =
                [
                    new StepContext
                    {
                        StepName = "step-a",
                        Status = StepExecutionStatus.Completed
                    }
                ]
            };

        checker.Validate(
            [candidate],
            context);

        Assert.False(candidate.HasErrors);
    }

    [Fact]
    public void Validate_WhenDependencySatisfiedByCandidate_RemainsValid()
    {
        var dependency =
            CreateRegistration<StepA>("step-a");

        var target =
            CreateRegistration<StepB>("step-b");

        var checker =
            CreateChecker(
                (typeof(StepB), [dependency]));

        var dependencyCandidate =
            CreateCandidate(dependency);

        var targetCandidate =
            CreateCandidate(target);

        checker.Validate(
            [
                dependencyCandidate,
                targetCandidate
            ],
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid()
            });

        Assert.False(targetCandidate.HasErrors);
    }

    [Fact]
    public void Validate_WhenDependencyCandidateIsInvalid_DependencyNotSatisfied()
    {
        var dependency =
            CreateRegistration<StepA>("step-a");

        var target =
            CreateRegistration<StepB>("step-b", [dependency]);

        var checker =
            CreateChecker(
                (typeof(StepB), [dependency]));

        var dependencyCandidate =
            new StepCandidate
            {
                StepName = "step-a",
                Registration = dependency,
                Status = StepCandidateStatus.Invalid,
                Step = new object()
            };

        dependencyCandidate.AddError(
            StepProcessingMessageCode.InvalidRequest,
            "invalid");

        var targetCandidate =
            CreateCandidate(target);

        checker.Validate(
            [
                dependencyCandidate,
                targetCandidate
            ],
            new ParticipantContext{ ParticipantProcessId = Guid.NewGuid() });

        Assert.Equal(
            StepCandidateStatus.Invalid,
            targetCandidate.Status);

        Assert.Contains(
            targetCandidate.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.DependencyNotSatisfied);
    }

    [Fact]
    public void Validate_WhenDependencyNotSatisfied_MarksCandidateInvalid()
    {
        var dependency =
            CreateRegistration<StepA>("step-a");

        var target =
            CreateRegistration<StepB>(
                "step-b",
                [dependency]);

        var checker =
            CreateChecker();

        var candidate =
            CreateCandidate(target);

        checker.Validate(
            [candidate],
            new ParticipantContext{ ParticipantProcessId = Guid.NewGuid() });

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.True(candidate.HasErrors);

        Assert.Contains(
            candidate.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.DependencyNotSatisfied);
    }

    [Fact]
    public void Validate_WhenMultipleDependenciesMissing_AddsErrorForEachDependency()
    {
        var stepA =
            CreateRegistration<StepA>("step-a");

        var stepB =
            CreateRegistration<StepB>("step-b");

        var stepC =
            CreateRegistration<StepC>("step-c", [stepA, stepB]);

        var checker =
            CreateChecker(
                (typeof(StepC), [stepA, stepB]));

        var candidate =
            CreateCandidate(stepC);

        checker.Validate(
            [candidate],
            new ParticipantContext{ ParticipantProcessId = Guid.NewGuid() });

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.Equal(
            2,
            candidate.Messages.Count(x =>
                x.Code ==
                StepProcessingMessageCode.DependencyNotSatisfied));
    }

    [Fact]
    public void Validate_WhenRepeatableStepPreviouslyCompleted_RemainsBuilt()
    {
        var checker =
            CreateChecker();

        var registration =
            CreateRegistration<StepA>(
                "step-a",
                repeatable: true);

        var candidate =
            CreateCandidate(
                registration);

        var context =
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid(),
                Steps =
                [
                    new StepContext
                {
                    StepName = "step-a",
                    Status = StepExecutionStatus.Completed
                }
                ]
            };

        checker.Validate(
            [candidate],
            context);

        Assert.Equal(
            StepCandidateStatus.Built,
            candidate.Status);
    }

    [Fact]
    public void Validate_WhenRepeatableStepPreviouslyCompleted_AddsRepeatableMessage()
    {
        var checker =
            CreateChecker();

        var registration =
            CreateRegistration<StepA>(
                "step-a",
                repeatable: true);

        var candidate =
            CreateCandidate(
                registration);

        var context =
            new ParticipantContext
            {
                ParticipantProcessId = Guid.NewGuid(),
                Steps =
                [
                    new StepContext
                {
                    StepName = "step-a",
                    Status = StepExecutionStatus.Completed
                }
                ]
            };

        checker.Validate(
            [candidate],
            context);

        Assert.Contains(
            candidate.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.RepeatableStep);
    }

    private static StepCandidateConsistencyChecker CreateChecker(
        params (Type StepType, ProcessStepRegistration[] Dependencies)[] registrations)
    {
        return new StepCandidateConsistencyChecker();
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name,
        IReadOnlyCollection<ProcessStepRegistration>? dependencies = null,
        bool repeatable = false)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(object),
            typeof(object),
            dependencies ?? [],
            [],
            [],
            repeatable ? new RepeatableOptions { Enabled = true } : new RepeatableOptions(),
           new ProcessStepMetadata(
                name,
                $"{name} description.",
                "1.0",
                $"{name} displayname"));
    }

    private static StepCandidate CreateCandidate(
        ProcessStepRegistration registration)
    {
        return new StepCandidate
        {
            StepName = registration.Metadata.Name,
            Registration = registration,
            Status = StepCandidateStatus.Built,
            Step = new object()
        };
    }

    private sealed class StepA;

    private sealed class StepB;

    private sealed class StepC;
}