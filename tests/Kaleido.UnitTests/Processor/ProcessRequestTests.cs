using Kaleido.Processor;

using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class ProcessRequestTests
    : SutFixture
{

    [ProcessStep(DisplayName = "My step", Description = "My step description.", Version = "1.0")]
    private sealed class MyStep : IProcessStep
    {
        public int Value { get; init; }
    }

    [Fact]
    public void ForStep_UsesStepTypeName()
    {
        var step = new MyStep { Value = 42 };

        var request = ProcessRequest.ForStep(step);

        Assert.Null(request.ProcessId);
        Assert.True(request.Processor.Steps.TryGetValue(nameof(MyStep), out var registeredStep));
        Assert.Same(step, registeredStep);
    }

    [Fact]
    public void ForStep_PassesProcessId()
    {
        var processId = Guid.NewGuid();
        var step = new MyStep { Value = 1 };

        var request = ProcessRequest.ForStep(step, processId);

        Assert.Equal(processId, request.ProcessId);
    }

    [Fact]
    public void ForStep_NullStep_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProcessRequest.ForStep<MyStep>(null!));
    }

    [Fact]
    public void ForStep_StepLookupIsCaseInsensitive()
    {
        var step = new MyStep { Value = 1 };

        var request = ProcessRequest.ForStep(step);

        Assert.True(request.Processor.Steps.ContainsKey("MYSTEP"));
        Assert.True(request.Processor.Steps.ContainsKey("mystep"));
        Assert.True(request.Processor.Steps.ContainsKey("MyStep"));
    }
}
