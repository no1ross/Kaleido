using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Moq;

namespace Kaleido.Process.UnitTests.Processor.Execution;

public sealed class StepAvailabilityResolverTests
{
    [Fact]
    public void Resolve_WhenCurrentCandidateIsNull_Throws()
    {
        var resolver = CreateResolver();

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
        var resolver = CreateResolver();

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
        var resolver = CreateResolver();

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

        var resolver = CreateResolver(nextRegistration);

        var result =
            resolver.Resolve(
                current,
                [next],
                CreateContext());

        AssertContainsStep(result, "step-b");
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

        var resolver = CreateResolver(dependentRegistration);

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext());

        AssertContainsStep(result, "step-b");
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

        var resolver = CreateResolver(completedRegistration, dependentRegistration);

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertContainsStep(result, "step-c");
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

        var resolver = CreateResolver();

        var result =
            resolver.Resolve(
                current,
                [dependent],
                CreateContext());

        AssertDoesNotContainStep(result, "step-c");
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

        var resolver = CreateResolver(completedRegistration);

        var result =
            resolver.Resolve(
                current,
                [completedCandidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertDoesNotContainStep(result, "step-a");
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

        var resolver = CreateResolver();

        var result =
            resolver.Resolve(
                current,
                [current],
                CreateContext());

        AssertDoesNotContainStep(result, "step-a");
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

        var resolver = CreateResolver(availableAfterRegistration);

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext());

        AssertContainsStep(result, "step-b");
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

        var resolver = CreateResolver(completedRegistration, availableAfterRegistration);

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertContainsStep(result, "step-c");
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

        var resolver = CreateResolver();

        var result =
            resolver.Resolve(
                current,
                [availableAfter],
                CreateContext());

        AssertDoesNotContainStep(result, "step-c");
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

        var resolver = CreateResolver(availableUntilRegistration);

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext());

        AssertContainsStep(result, "step-c");
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

        var resolver = CreateResolver(completedRegistration);

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertDoesNotContainStep(result, "step-c");
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

        var resolver = CreateResolver();

        var result =
            resolver.Resolve(
                current,
                [availableUntil],
                CreateContext());

        AssertDoesNotContainStep(result, "step-b");
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

        var resolver = CreateResolver(dependencyRegistration, availableAfterRegistration, candidateRegistration);

        var result =
            resolver.Resolve(
                current,
                [candidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed),
                    ("step-b", StepExecutionStatus.Completed)));

        AssertContainsStep(result, "step-e");
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

        var resolver = CreateResolver(dependencyRegistration);

        var result =
            resolver.Resolve(
                current,
                [candidate],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertDoesNotContainStep(result, "step-d");
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

        var resolver = CreateResolver(currentRegistration, registration1, registration2);

        var result =
            resolver.Resolve(
                current,
                [candidate1, candidate2],
                CreateContext());

        Assert.Single(result);
        AssertContainsStep(result, "step-b");
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

        var resolver = CreateResolver(registration);

        var result =
            resolver.Resolve(
                candidate,
                [],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed)));

        AssertContainsStep(result, "step-a");
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

        var resolver = CreateResolver(closedRegistration, repeatableRegistration);

        var result =
            resolver.Resolve(
                candidate,
                [],
                CreateContext(
                    ("step-a", StepExecutionStatus.Completed),
                    ("step-b", StepExecutionStatus.Completed)));

        AssertDoesNotContainStep(result, "step-b");
    }

    [Fact]
    public void Resolve_ReturnsReferencesWithCurrentProcessorName()
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

        var resolver = CreateResolver(nextRegistration);

        var result =
            resolver.Resolve(
                current,
                [],
                CreateContext());

        Assert.All(
            result,
            x => Assert.Equal(TestProcessorName, x.ProcessorName));
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
                "1.0.0",
                $"{name} displayname"));
    }

    private static ProcessorContext CreateContext(
        params (string StepName, StepExecutionStatus Status)[] steps)
    {
        return new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
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

    private const string TestProcessorName = "test-processor";

    private static StepAvailabilityResolver CreateResolver(
        params ProcessStepRegistration[] registrations)
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns(registrations);

        foreach (var registration in registrations)
        {
            registry
                .Setup(x =>
                    x.Find(
                        registration.Metadata.Name))
                .Returns(registration);
        }

        var processorRegistryItem = new ProcessorRegistryItem
        {
            Name = TestProcessorName,
            Description = "test",
            DisplayName = "Test Processor",
            Version = "1.0"
        };

        var processorRegistry =
            new Mock<IProcessorRegistry>();

        processorRegistry
            .Setup(x => x.Registrations)
            .Returns([processorRegistryItem]);

        return new StepAvailabilityResolver(
            registry.Object,
            processorRegistry.Object);
    }

    /// <summary>
    /// Checks that the result contains a reference with the given step name.
    /// </summary>
    private static void AssertContainsStep(
        IReadOnlyCollection<ProcessStepReference> result,
        string stepName)
    {
        Assert.Contains(
            result,
            x => string.Equals(x.StepName, stepName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks that the result does not contain a reference with the given step name.
    /// </summary>
    private static void AssertDoesNotContainStep(
        IReadOnlyCollection<ProcessStepReference> result,
        string stepName)
    {
        Assert.DoesNotContain(
            result,
            x => string.Equals(x.StepName, stepName, StringComparison.OrdinalIgnoreCase));
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
