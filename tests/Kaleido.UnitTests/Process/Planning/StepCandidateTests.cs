using Kaleido.Process.Planning;

namespace Kaleido.Process.UnitTests.Planning;

public sealed class StepCandidateTests
{
    private static StepCandidate CreateSut(string stepName = "test-step") =>
        new() { StepName = stepName };

    [Fact]
    public void Messages_InitiallyEmpty()
    {
        Assert.Empty(CreateSut().Messages);
    }

    [Fact]
    public void HasErrors_WhenNoMessages_ReturnsFalse()
    {
        Assert.False(CreateSut().HasErrors);
    }

    [Fact]
    public void Status_DefaultsTo_Pending()
    {
        Assert.Equal(StepCandidateStatus.Pending, CreateSut().Status);
    }

    [Fact]
    public void AddError_AddsMessageAndSetsHasErrors()
    {
        var sut = CreateSut();
        sut.AddError(StepProcessingMessageCode.UnknownStep, "test error");

        Assert.True(sut.HasErrors);
        Assert.Single(sut.Messages);
    }

    [Fact]
    public void AddWarning_AddsMessageButDoesNotSetHasErrors()
    {
        var sut = CreateSut();
        sut.AddWarning(StepProcessingMessageCode.UnknownStep, "test warning");

        Assert.False(sut.HasErrors);
        Assert.Single(sut.Messages);
    }

    [Fact]
    public void AddInformation_AddsMessageButDoesNotSetHasErrors()
    {
        var sut = CreateSut();
        sut.AddInformation(StepProcessingMessageCode.UnknownStep, "test info");

        Assert.False(sut.HasErrors);
        Assert.Single(sut.Messages);
    }

    [Fact]
    public void MarkInvalid_SetsStatusToInvalidAndAddsError()
    {
        var sut = CreateSut();
        sut.MarkInvalid(StepProcessingMessageCode.UnknownStep, "bad step");

        Assert.Equal(StepCandidateStatus.Invalid, sut.Status);
        Assert.True(sut.HasErrors);
    }

    [Fact]
    public void Invalid_StaticFactory_ReturnsInvalidCandidateWithError()
    {
        var candidate = StepCandidate.Invalid(
            "missing-step",
            StepProcessingMessageCode.UnknownStep,
            "not registered");

        Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
        Assert.True(candidate.HasErrors);
        Assert.Equal("missing-step", candidate.StepName);
    }

    [Fact]
    public void GetStep_WhenStepIsCorrectType_ReturnsStep()
    {
        var sut = CreateSut();
        var step = new object();
        sut.Step = step;

        Assert.Equal(step, sut.GetStep<object>());
    }

    [Fact]
    public void GetStep_WhenStepIsWrongType_ThrowsKaleidoFrameworkException()
    {
        var sut = CreateSut();
        sut.Step = "a string";

        Assert.Throws<Kaleido.Exceptions.KaleidoFrameworkException>(
            () => sut.GetStep<object[]>());
    }
}
