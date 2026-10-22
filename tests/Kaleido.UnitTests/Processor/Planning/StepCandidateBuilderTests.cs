using Kaleido.Processor.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Planning.UnitTests;

public sealed class StepCandidateBuilderTests
    : SutFixture
{
    private readonly Mock<IProcessorStepRegistry> _registry = new();

    private StepCandidateBuilder Sut => CreateSut();

    private StepCandidateBuilder CreateSut() =>
        new(_registry.Object);

    [Fact]
    public void Build_WhenRequestIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Sut.Build(null!));
    }

    [Fact]
    public void Build_WhenStepNotFound_MarksCandidateInvalid()
    {
        _registry.Setup(r => r.Find("unknown-step")).Returns((ProcessStepRegistration?)null);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["unknown-step"] = new { }
            }
        };

        var candidates = Sut.Build(request);

        Assert.Single(candidates);
        Assert.Equal("unknown-step", candidates.First().StepName);
        Assert.Equal(StepCandidateStatus.Invalid, candidates.First().Status);
        Assert.True(candidates.First().HasErrors);
    }

    [Fact]
    public void Build_WhenStepFound_CreatesCandidateWithRegistration()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "test-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = new TestStep { Name = "test" }
            }
        };

        var candidates = Sut.Build(request);

        Assert.Single(candidates);
        Assert.Equal("test-step", candidates.First().StepName);
        Assert.Same(registration, candidates.First().Registration);
        Assert.Equal(StepCandidateStatus.Built, candidates.First().Status);
    }

    [Fact]
    public void Build_WhenTheValueIsNotTheStepType_MarksCandidateInvalid()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "test-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = "invalid-json-data"
            }
        };

        var candidates = Sut.Build(request);

        Assert.Single(candidates);
        Assert.Equal("test-step", candidates.First().StepName);
        Assert.Equal(StepCandidateStatus.Invalid, candidates.First().Status);
        Assert.True(candidates.First().HasErrors);
    }

    [Fact]
    public void Build_WhenTheValueIsNull_MarksCandidateInvalid()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "test-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = null
            }
        };

        var candidates = Sut.Build(request);

        Assert.Single(candidates);
        Assert.Equal("test-step", candidates.First().StepName);
        Assert.Equal(StepCandidateStatus.Invalid, candidates.First().Status);
        Assert.True(candidates.First().HasErrors);
    }

    [Fact]
    public void Build_WhenMultipleSteps_BuildsAllCandidates()
    {
        var registrationA = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "step-a", Description = "desc", Version = "1.0", DisplayName = "display" });

        var registrationB = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "step-b", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Find("step-a")).Returns(registrationA);
        _registry.Setup(r => r.Find("step-b")).Returns(registrationB);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["step-a"] = new TestStep { Name = "a" },
                ["step-b"] = new TestStep { Name = "b" }
            }
        };

        var candidates = Sut.Build(request);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("step-a", candidates.First().StepName);
        Assert.Equal("step-b", candidates.Last().StepName);
        Assert.All(candidates, c => Assert.Equal(StepCandidateStatus.Built, c.Status));
    }

    private ProcessStepRegistration RegisterTestStep()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "test-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        return registration;
    }

    // Core never parses input (#344): a transport reads its wire format into the step type first.
    [Fact]
    public void Build_FromAJsonElement_IsInvalid_BecauseCoreNeverParsesInput()
    {
        RegisterTestStep();
        using var json = System.Text.Json.JsonDocument.Parse("{\"name\":\"from-json\"}");

        var candidate = Assert.Single(Sut.Build(new ProcessorRequest { Steps = new Dictionary<string, object?> { ["test-step"] = json.RootElement.Clone() } }));

        Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
        Assert.Null(candidate.Step);
    }

    [Fact]
    public void Build_FromAnInstanceOfTheStepType_UsesItAsIs()
    {
        RegisterTestStep();
        var step = new TestStep { Name = "typed" };

        var candidate = Assert.Single(Sut.Build(new ProcessorRequest { Steps = new Dictionary<string, object?> { ["test-step"] = step } }));

        Assert.Same(step, candidate.Step);
    }

    [ProcessStep(DisplayName = "test-step", Description = "test-step", Version = "1.0")]
    private sealed class TestStep : IProcessStep
    {
        public string? Name { get; init; }
    }
}
