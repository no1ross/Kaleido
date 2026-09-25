using Kaleido.Process;

namespace Kaleido.Process.UnitTests.Processor;

public sealed class ProcessRequestTests
{
    [ProcessStep(Name = "my-step", Version = "1.0")]
    private sealed class StepWithAttribute
    {
        public int Value { get; init; }
    }

    private sealed class StepWithoutAttribute
    {
        public int Value { get; init; }
    }

    [Fact]
    public void ForStep_WithAttribute_UsesAttributeName()
    {
        var step = new StepWithAttribute { Value = 42 };

        var request = ProcessRequest.ForStep(step);

        Assert.Null(request.ProcessId);
        Assert.True(request.Processor.Steps.ContainsKey("my-step"));
        Assert.Same(step, request.Processor.Steps["my-step"]);
    }

    [Fact]
    public void ForStep_WithAttribute_PassesProcessId()
    {
        var processId = Guid.NewGuid();
        var step = new StepWithAttribute { Value = 1 };

        var request = ProcessRequest.ForStep(step, processId);

        Assert.Equal(processId, request.ProcessId);
    }

    [Fact]
    public void ForStep_WithoutAttribute_FallsBackToTypeName()
    {
        var step = new StepWithoutAttribute { Value = 7 };

        var request = ProcessRequest.ForStep(step);

        Assert.True(request.Processor.Steps.ContainsKey("StepWithoutAttribute"));
        Assert.Same(step, request.Processor.Steps["StepWithoutAttribute"]);
    }

    [Fact]
    public void ForStep_NullStep_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProcessRequest.ForStep<StepWithAttribute>(null!));
    }

    [Fact]
    public void ForStep_StepLookupIsCaseInsensitive()
    {
        var step = new StepWithAttribute { Value = 1 };

        var request = ProcessRequest.ForStep(step);

        Assert.True(request.Processor.Steps.ContainsKey("MY-STEP"));
        Assert.True(request.Processor.Steps.ContainsKey("my-step"));
        Assert.True(request.Processor.Steps.ContainsKey("My-Step"));
    }
}
