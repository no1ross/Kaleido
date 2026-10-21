namespace Kaleido.Processor.Execution.UnitTests;

// The untyped result and the factories every handler starts from. The typed result's own members
// (Next, RequireInformation, the conversion) are in ProcessStepHandlerResultOfTTests.
public sealed class ProcessStepHandlerResultTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ProcessMessage Note =
        new() { Code = "note", Type = MessageType.Information, Message = "A note." };

    private static readonly InformationRequest Request =
        new() { InformationRequestId = "questions" };

    private static ProcessStepHandlerResult CreateSut() => ProcessStepHandlerResult.Success(Note);

    [Fact]
    public void Success_IsASuccessWithoutARequiredStep()
    {
        var result = CreateSut();

        Assert.True(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void SuccessWithNextStep_RequiresThatStep()
    {
        var result = ProcessStepHandlerResult.Success<NextStep>(Note);

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(NextStep), result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_RequiresTheInformationStepWithItsRequest()
    {
        var result = ProcessStepHandlerResult.RequireInformation<AnswerStep>(Request, Note);

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_WithoutARequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessStepHandlerResult.RequireInformation<AnswerStep>(null!));
    }

    [Fact]
    public void Failure_IsANonRejectedFailureWithItsMessages()
    {
        var result = ProcessStepHandlerResult.Failure(Note);

        Assert.False(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.Response);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void Reject_IsARejectedFailure()
    {
        var result = ProcessStepHandlerResult.Reject(Note);

        Assert.False(result.Succeeded);
        Assert.True(result.Rejected);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void HandOff_IsASuccessForTheTargetProcessor()
    {
        var result = ProcessStepHandlerResult.HandOff("billing", Note);

        Assert.True(result.Succeeded);
        Assert.Equal("billing", result.TargetProcessorName);
        Assert.Null(result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void SuccessWithResponse_IsATypedSuccess()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.Success(new Response("ok"), Note);

        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Response?.Value);
        Assert.Null(result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void FailureWithResponse_IsATypedNonRejectedFailure()
    {
        var result = ProcessStepHandlerResult.Failure(new Response("no"), Note);

        Assert.False(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Equal("no", result.Response?.Value);
        Assert.Equal([Note], result.Messages);
    }

    private sealed record Response(string Value);

    private sealed record NextStep : IProcessStep;

    private sealed record AnswerStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
