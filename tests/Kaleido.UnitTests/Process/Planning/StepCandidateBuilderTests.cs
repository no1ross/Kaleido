using Kaleido.Process.Attributes;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Planning;

public sealed class StepCandidateBuilderTests
{
    private readonly Mock<IProcessStepRegistry> _registry = new();
    private readonly StepCandidateBuilder _builder;

    public StepCandidateBuilderTests()
    {
        _builder = new StepCandidateBuilder(_registry.Object);
    }

    [Fact]
    public void Build_WhenRequestIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _builder.Build(null!));
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

        var candidates = _builder.Build(request);

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
            new ProcessStepMetadata("test-step", "desc", "1.0", "display"));

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = new { Name = "test" }
            }
        };

        var candidates = _builder.Build(request);

        Assert.Single(candidates);
        Assert.Equal("test-step", candidates.First().StepName);
        Assert.Same(registration, candidates.First().Registration);
        Assert.Equal(StepCandidateStatus.Built, candidates.First().Status);
    }

    [Fact]
    public void Build_WhenDeserializationFails_MarksCandidateInvalid()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("test-step", "desc", "1.0", "display"));

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = "invalid-json-data"
            }
        };

        var candidates = _builder.Build(request);

        Assert.Single(candidates);
        Assert.Equal("test-step", candidates.First().StepName);
        Assert.Equal(StepCandidateStatus.Invalid, candidates.First().Status);
        Assert.True(candidates.First().HasErrors);
    }

    [Fact]
    public void Build_WhenDeserializationReturnsNull_MarksCandidateInvalid()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("test-step", "desc", "1.0", "display"));

        _registry.Setup(r => r.Find("test-step")).Returns(registration);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["test-step"] = null
            }
        };

        var candidates = _builder.Build(request);

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
            new ProcessStepMetadata("step-a", "desc", "1.0", "display"));

        var registrationB = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("step-b", "desc", "1.0", "display"));

        _registry.Setup(r => r.Find("step-a")).Returns(registrationA);
        _registry.Setup(r => r.Find("step-b")).Returns(registrationB);

        var request = new ProcessorRequest
        {
            Steps = new Dictionary<string, object?>
            {
                ["step-a"] = new { Name = "a" },
                ["step-b"] = new { Name = "b" }
            }
        };

        var candidates = _builder.Build(request);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("step-a", candidates.First().StepName);
        Assert.Equal("step-b", candidates.Last().StepName);
        Assert.All(candidates, c => Assert.Equal(StepCandidateStatus.Built, c.Status));
    }

    [ProcessStep(Name = "test-step", Version = "1.0")]
    private sealed class TestStep
    {
        public string? Name { get; init; }
    }
}
