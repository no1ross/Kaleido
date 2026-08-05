using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.UnitTests.Participant.Execution;

public sealed class StepAvailabilityResolverTests
{
    [Fact]
    public void Resolve_WhenCurrentCandidateIsNull_Throws()
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var context =
            CreateContext();

        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(
                null!,
                [],
                context));
    }

    [Fact]
    public void Resolve_WhenCandidatesIsNull_Throws()
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var current =
            CreateCandidate(
                "step-a",
                CreateRegistration<TestStepA>(
                    "step-a"));

        var context =
            CreateContext();

        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(
                current,
                null!,
                context));
    }

    [Fact]
    public void Resolve_WhenContextIsNull_Throws()
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var current =
            CreateCandidate(
                "step-a",
                CreateRegistration<TestStepA>(
                    "step-a"));

        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(
                current,
                [],
                null!));
    }

    [Fact]
    public void Resolve_WhenCandidateHasNoDependencies_ReturnsCandidate()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var nextRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var next =
            CreateCandidate(
                "step-b",
                nextRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [next],
                CreateContext());

        Assert.Contains(
            "step-b",
            result);
    }

    [Fact]
    public void Resolve_WhenDependencyIsCurrentCandidate_ReturnsCandidate()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var dependentRegistration =
            CreateRegistration<TestStepB>(
                "step-b",
                dependencies:
                [
                    currentRegistration
                ]);

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var dependent =
            CreateCandidate(
                "step-b",
                dependentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext());

        Assert.Contains(
            "step-b",
            result);
    }

    [Fact]
    public void Resolve_WhenDependencyIsCompletedInContext_ReturnsCandidate()
    {
        var completedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var dependentRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                dependencies:
                [
                    completedRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var dependent =
            CreateCandidate(
                "step-c",
                dependentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(completedRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.Contains(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenDependencyIsNotCompleted_DoesNotReturnCandidate()
    {
        var missingDependencyRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var dependentRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                dependencies:
                [
                    missingDependencyRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var dependent =
            CreateCandidate(
                "step-c",
                dependentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext());

        Assert.DoesNotContain(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenStepAlreadyCompleted_DoesNotReturnCandidate()
    {
        var completedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var completedCandidate =
            CreateCandidate(
                "step-a",
                completedRegistration);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(completedRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [completedCandidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.DoesNotContain(
            "step-a",
            result);
    }

    [Fact]
    public void Resolve_WhenCandidateIsCurrentCandidate_DoesNotReturnCurrentCandidate()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [current],
                CreateContext());

        Assert.DoesNotContain(
            "step-a",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableAfterIsCurrentCandidate_ReturnsCandidate()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var availableAfterRegistration =
            CreateRegistration<TestStepB>(
                "step-b",
                availableAfter:
                [
                    currentRegistration
                ]);

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var availableAfter =
            CreateCandidate(
                "step-b",
                availableAfterRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext());

        Assert.Contains(
            "step-b",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableAfterIsCompletedInContext_ReturnsCandidate()
    {
        var completedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var availableAfterRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                availableAfter:
                [
                    completedRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var availableAfter =
            CreateCandidate(
                "step-c",
                availableAfterRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(completedRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.Contains(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableAfterIsNotCompleted_DoesNotReturnCandidate()
    {
        var requiredPriorRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var availableAfterRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                availableAfter:
                [
                    requiredPriorRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var availableAfter =
            CreateCandidate(
                "step-c",
                availableAfterRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext());

        Assert.DoesNotContain(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableUntilIsNotCompleted_ReturnsCandidate()
    {
        var untilRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var availableUntilRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                availableUntil:
                [
                    untilRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var availableUntil =
            CreateCandidate(
                "step-c",
                availableUntilRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext());

        Assert.Contains(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableUntilIsCompletedInContext_DoesNotReturnCandidate()
    {
        var completedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var availableUntilRegistration =
            CreateRegistration<TestStepC>(
                "step-c",
                availableUntil:
                [
                    completedRegistration
                ]);

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var availableUntil =
            CreateCandidate(
                "step-c",
                availableUntilRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(completedRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.DoesNotContain(
            "step-c",
            result);
    }

    [Fact]
    public void Resolve_WhenAvailableUntilIsCurrentCandidate_DoesNotReturnCandidate()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var availableUntilRegistration =
            CreateRegistration<TestStepB>(
                "step-b",
                availableUntil:
                [
                    currentRegistration
                ]);

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var availableUntil =
            CreateCandidate(
                "step-b",
                availableUntilRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext());

        Assert.DoesNotContain(
            "step-b",
            result);
    }

    [Fact]
    public void Resolve_WhenMultipleRulesAreSatisfied_ReturnsCandidate()
    {
        var dependencyRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var availableAfterRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var currentRegistration =
            CreateRegistration<TestStepC>(
                "step-c");

        var availableUntilRegistration =
            CreateRegistration<TestStepD>(
                "step-d");

        var candidateRegistration =
            CreateRegistration<TestStepE>(
                "step-e",
                dependencies:
                [
                    dependencyRegistration
                ],
                availableAfter:
                [
                    availableAfterRegistration
                ],
                availableUntil:
                [
                    availableUntilRegistration
                ]);

        var current =
            CreateCandidate(
                "step-c",
                currentRegistration);

        var candidate =
            CreateCandidate(
                "step-e",
                candidateRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(dependencyRegistration);

        registry
            .Setup(x =>
                x.Find("step-b"))
            .Returns(availableAfterRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [candidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed),
                    ("step-b", StepExecutionStatus.Completed)));

        Assert.Contains(
            "step-e",
            result);
    }

    [Fact]
    public void Resolve_WhenAnyRuleIsNotSatisfied_DoesNotReturnCandidate()
    {
        var dependencyRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var availableAfterRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var currentRegistration =
            CreateRegistration<TestStepC>(
                "step-c");

        var candidateRegistration =
            CreateRegistration<TestStepD>(
                "step-d",
                dependencies:
                [
                    dependencyRegistration
                ],
                availableAfter:
                [
                    availableAfterRegistration
                ]);

        var current =
            CreateCandidate(
                "step-c",
                currentRegistration);

        var candidate =
            CreateCandidate(
                "step-d",
                candidateRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(dependencyRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [candidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.DoesNotContain(
            "step-d",
            result);
    }

    [Fact]
    public void Resolve_WhenDuplicateStepNamesExist_ReturnsDistinctNames()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var registration1 =
            CreateRegistration<TestStepB>(
                "step-b");

        var registration2 =
            CreateRegistration<TestStepC>(
                "STEP-B");

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var candidate1 =
            CreateCandidate(
                "step-b",
                registration1);

        var candidate2 =
            CreateCandidate(
                "STEP-B",
                registration2);

        var registry =
            new Mock<IProcessStepRegistry>();

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [candidate1, candidate2],
                CreateContext());

        Assert.Single(result);
        Assert.Contains(
            "step-b",
            result,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_WhenCompletedStepRegistrationIsFound_AddsItToScopedRegistrations()
    {
        var completedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var currentRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var current =
            CreateCandidate(
                "step-b",
                currentRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(completedRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        _ = resolver.Resolve(
            current,
            [],
            CreateContext(
                ("step-a", StepExecutionStatus.Completed)));

        registry.Verify(
            x => x.Find("step-a"),
            Times.Once);
    }

    [Fact]
    public void Resolve_WhenCompletedStepRegistrationIsNotFound_IgnoresIt()
    {
        var currentRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var candidateRegistration =
            CreateRegistration<TestStepB>(
                "step-b");

        var current =
            CreateCandidate(
                "step-a",
                currentRegistration);

        var candidate =
            CreateCandidate(
                "step-b",
                candidateRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("missing-step"))
            .Returns((ProcessStepRegistration?)null);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                current,
                [candidate],
                CreateContext(
                    ("missing-step", StepExecutionStatus.Completed)));

        Assert.Contains(
            "step-b",
            result);
    }

    [Fact]
    public void Resolve_WhenRepeatableStepPreviouslyCompleted_ReturnsStep()
    {
        var registration =
            CreateRegistration<TestStepA>(
                "step-a",
                repeatable: true);

        var candidate =
            CreateCandidate(
                "step-a",
                registration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(registration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                candidate,
                [],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        Assert.Contains(
            "step-a",
            result);
    }

    [Fact]
    public void Resolve_WhenRepeatableStepPreviouslyCompleted_StillRespectsAvailableUntil()
    {
        var closedRegistration =
            CreateRegistration<TestStepA>(
                "step-a");

        var repeatableRegistration =
            CreateRegistration<TestStepB>(
                "step-b",
                availableUntil:
                [
                    closedRegistration
                ],
                repeatable: true);

        var candidate =
            CreateCandidate(
                "step-b",
                repeatableRegistration);

        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x =>
                x.Find("step-a"))
            .Returns(closedRegistration);

        registry
            .Setup(x =>
                x.Find("step-b"))
            .Returns(repeatableRegistration);

        var resolver =
            new StepAvailabilityResolver(
                registry.Object);

        var result =
            resolver.Resolve(
                candidate,
                [],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed),
                    ("step-b", StepExecutionStatus.Completed)));

        Assert.DoesNotContain(
            "step-b",
            result);
    }

    private static StepCandidate CreateCandidate(
        string stepName,
        ProcessStepRegistration? registration)
    {
        return new StepCandidate
        {
            StepName = stepName,
            Registration = registration,
            Step = new object()
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name,
        IReadOnlyCollection<ProcessStepRegistration>? dependencies = null,
        IReadOnlyCollection<ProcessStepRegistration>? availableAfter = null,
        IReadOnlyCollection<ProcessStepRegistration>? availableUntil = null,
        bool repeatable = false)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(TestStepResponse),
            typeof(TestStepHandler),
            dependencies ?? [],
            availableAfter ?? [],
            availableUntil ?? [],
            new RepeatableOptions
            {
                Enabled = repeatable
            },
            new ProcessStepMetadata(
                name,
                $"{name} description",
                "1.0.0"));
    }

    private static ParticipantContext CreateContext(
        params (string StepName, StepExecutionStatus Status)[] steps)
    {
        return new ParticipantContext
        {
            Steps =
                steps
                    .Select(x =>
                        new StepContext
                        {
                            StepName = x.StepName,
                            Status = x.Status
                        })
                    .ToArray()
        };
    }

    private sealed class TestStepA
    {
    }

    private sealed class TestStepB
    {
    }

    private sealed class TestStepC
    {
    }

    private sealed class TestStepD
    {
    }

    private sealed class TestStepE
    {
    }

    private sealed class TestStepResponse
    {
    }

    private sealed class TestStepHandler
    {
    }
}
