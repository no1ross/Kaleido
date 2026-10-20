using Kaleido.UnitTests;

namespace Kaleido.Processor.Context.UnitTests;

public sealed class ProcessorContextTests
    : SutFixture
{
    private static ProcessorContext CreateSut(params StepContext[] steps) =>
        new() { ProcessId = Guid.NewGuid(), ProcessorName = "intake", Steps = steps };

    private static StepContext Step(string name, StepExecutionStatus status) =>
        new() { StepName = name, Version = "1.0.0", Status = status };

    [Theory]
    [InlineData("CaptureMember")]
    [InlineData("capturemember")]
    public void FindStep_ByNameIgnoringCase_ReturnsTheStep(string name)
    {
        var step = Step("CaptureMember", StepExecutionStatus.Completed);

        Assert.Same(step, CreateSut(step).FindStep(name));
    }

    [Fact]
    public void FindStep_ForAStepNotInTheProcess_ReturnsNull()
    {
        Assert.Null(CreateSut(Step("CaptureMember", StepExecutionStatus.Completed)).FindStep("CaptureService"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FindStep_WithoutAName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateSut().FindStep(name!));
    }

    [Theory]
    [InlineData(StepExecutionStatus.Completed, true)]
    [InlineData(StepExecutionStatus.Failed, false)]
    [InlineData(StepExecutionStatus.Canceled, false)]
    public void HasCompletedStep_IsTrueOnlyForACompletedStep(StepExecutionStatus status, bool expected)
    {
        Assert.Equal(expected, CreateSut(Step("CaptureMember", status)).HasCompletedStep("capturemember"));
    }

    [Fact]
    public void HasCompletedStep_ForAStepNotInTheProcess_IsFalse()
    {
        Assert.False(CreateSut(Step("CaptureMember", StepExecutionStatus.Completed)).HasCompletedStep("CaptureService"));
    }
}
