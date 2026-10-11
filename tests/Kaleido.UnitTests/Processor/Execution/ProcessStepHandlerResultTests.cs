using Kaleido.Exceptions;

namespace Kaleido.Processor.Execution.UnitTests;

public sealed class ProcessStepHandlerResultTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ProcessMessage Note =
        new() { Code = "note", Type = MessageType.Information, Message = "A note." };

    private static readonly InformationRequest Request =
        new() { InformationRequestId = "questions" };

    [Fact]
    public void Success_WithResponse_InfersTheTypedResult()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.Success(new Response("ok"), Note);

        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Response!.Value);
        Assert.Null(result.RequiredStep);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void Next_OnASuccess_RequiresTheStepAndKeepsTheRest()
    {
        var result = ProcessStepHandlerResult.Success(new Response("ok"), Note).Next<NextStep>();

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(NextStep), result.RequiredStep);
        Assert.Equal("ok", result.Response!.Value);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_OnASuccess_RequiresTheInformationStepWithItsRequest()
    {
        var result = ProcessStepHandlerResult.Success(new Response("ok")).RequireInformation<AnswerStep>(Request);

        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
        Assert.Equal("ok", result.Response!.Value);
    }

    [Fact]
    public void RequireInformation_WhenRequestIsNull_Throws()
    {
        var success = ProcessStepHandlerResult.Success(new Response("ok"));

        Assert.Throws<ArgumentNullException>(() => success.RequireInformation<AnswerStep>(null!));
    }

    [Fact]
    public void Next_OnAFailure_Throws()
    {
        var failure = ProcessStepHandlerResult.Failure(new Response("no"));

        var exception = Assert.Throws<KaleidoFrameworkException>(() => failure.Next<NextStep>());

        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, exception.Code);
    }

    [Fact]
    public void Next_OnARejection_Throws()
    {
        ProcessStepHandlerResult<Response> rejected = ProcessStepHandlerResult.Reject(Note);

        Assert.Throws<KaleidoFrameworkException>(() => rejected.Next<NextStep>());
    }

    [Fact]
    public void RequireInformation_OnAHandOff_Throws()
    {
        ProcessStepHandlerResult<Response> handOff = ProcessStepHandlerResult.HandOff("billing");

        Assert.Throws<KaleidoFrameworkException>(() => handOff.RequireInformation<AnswerStep>(Request));
    }

    [Fact]
    public void Failure_WithResponse_InfersTheTypedResult()
    {
        var result = ProcessStepHandlerResult.Failure(new Response("no"), Note);

        Assert.False(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Equal("no", result.Response!.Value);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void Conversion_FromARejection_KeepsItsOutcome()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.Reject(Note);

        Assert.False(result.Succeeded);
        Assert.True(result.Rejected);
        Assert.Null(result.Response);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void Conversion_FromAHandOff_KeepsTheTargetProcessor()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.HandOff("billing", Note);

        Assert.True(result.Succeeded);
        Assert.Equal("billing", result.TargetProcessorName);
    }

    [Fact]
    public void Conversion_FromAnUntypedSuccessWithNextStep_KeepsTheRequiredStep()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.Success<NextStep>();

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(NextStep), result.RequiredStep);
    }

    [Fact]
    public void Conversion_FromAnUntypedRequireInformation_KeepsTheRequest()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.RequireInformation<AnswerStep>(Request);

        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
    }

    [Fact]
    public void Conversion_FromAResultWithAResponseOfAnotherType_Throws()
    {
        var untyped = ProcessStepHandlerResult.Success() with { Response = 42 };

        var exception = Assert.Throws<KaleidoFrameworkException>(() =>
        {
            ProcessStepHandlerResult<Response> converted = untyped;
            return converted;
        });

        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, exception.Code);
    }

    [Fact]
    public void Conversion_FromAResultWithAResponseOfTheSameType_KeepsIt()
    {
        var untyped = ProcessStepHandlerResult.Success() with { Response = new Response("ok") };

        ProcessStepHandlerResult<Response> converted = untyped;

        Assert.Equal("ok", converted.Response!.Value);
    }

    private sealed record Response(string Value);

    private sealed record NextStep : IProcessStep;

    private sealed record AnswerStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
