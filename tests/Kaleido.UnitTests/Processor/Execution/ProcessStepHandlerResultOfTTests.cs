using Kaleido.Exceptions;

namespace Kaleido.Processor.Execution.UnitTests;

// The typed result: requiring the next step, and the conversion from an untyped result.
public sealed class ProcessStepHandlerResultOfTTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ProcessMessage Note =
        new() { Code = "note", Type = MessageType.Information, Message = "A note." };

    private static readonly InformationRequest Request =
        new() { InformationRequestId = "questions" };

    private static ProcessStepHandlerResult<Response> CreateSut(bool succeeded = true, bool rejected = false, string? target = null) =>
        new() { Succeeded = succeeded, Rejected = rejected, TargetProcessorName = target, Response = new Response("ok"), Messages = [Note] };

    private static ProcessStepHandlerResult Untyped(bool succeeded = true, bool rejected = false, string? target = null, object? response = null) =>
        new() { Succeeded = succeeded, Rejected = rejected, TargetProcessorName = target, Response = response, Messages = [Note] };

    [Fact]
    public void Next_OnASuccess_RequiresTheStepAndKeepsTheRest()
    {
        var result = CreateSut().Next<NextStep>();

        Assert.True(result.Succeeded);
        Assert.Equal(typeof(NextStep), result.RequiredStep);
        Assert.Equal("ok", result.Response?.Value);
        Assert.Equal([Note], result.Messages);
    }

    [Fact]
    public void RequireInformation_OnASuccess_RequiresTheInformationStepWithItsRequest()
    {
        var result = CreateSut().RequireInformation<AnswerStep>(Request);

        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
        Assert.Equal("ok", result.Response?.Value);
    }

    [Fact]
    public void RequireInformation_WithoutARequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().RequireInformation<AnswerStep>(null!));
    }

    // Only a success in the current processor can require a next step.
    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, false, "billing")]
    public void RequiringANextStep_OnAnythingButASuccessHere_Throws(bool succeeded, bool rejected, string? target)
    {
        var sut = CreateSut(succeeded, rejected, target);

        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, Assert.Throws<KaleidoFrameworkException>(sut.Next<NextStep>).Code);
        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, Assert.Throws<KaleidoFrameworkException>(() => sut.RequireInformation<AnswerStep>(Request)).Code);
    }

    // A result that completes the process cannot also require a next step (#534).
    [Fact]
    public void RequiringANextStep_OnAResultThatCompletesTheProcess_Throws()
    {
        var sut = ProcessStepHandlerResult.Complete(new Response("done"));

        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, Assert.Throws<KaleidoFrameworkException>(sut.Next<NextStep>).Code);
        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, Assert.Throws<KaleidoFrameworkException>(() => sut.RequireInformation<AnswerStep>(Request)).Code);
    }

    [Fact]
    public void Conversion_KeepsThatTheResultCompletesTheProcess()
    {
        ProcessStepHandlerResult<Response> result = ProcessStepHandlerResult.Complete();

        Assert.True(result.CompletesProcess);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void TheResponse_IsTheUntypedResponseToo()
    {
        IProcessStepHandlerResult result = CreateSut();

        Assert.Equal(new Response("ok"), result.Response);
    }

    [Fact]
    public void Conversion_KeepsTheOutcomeStepsRequestTargetAndMessages()
    {
        var untyped = Untyped(rejected: true, target: "billing") with { RequiredStep = typeof(AnswerStep), InformationRequest = Request };

        ProcessStepHandlerResult<Response> result = untyped;

        Assert.True(result.Succeeded);
        Assert.True(result.Rejected);
        Assert.Equal("billing", result.TargetProcessorName);
        Assert.Equal(typeof(AnswerStep), result.RequiredStep);
        Assert.Same(Request, result.InformationRequest);
        Assert.Equal([Note], result.Messages);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Conversion_KeepsAResponseOfTheSameType()
    {
        ProcessStepHandlerResult<Response> result = Untyped(response: new Response("ok"));

        Assert.Equal("ok", result.Response?.Value);
    }

    [Fact]
    public void Conversion_OfAResponseOfAnotherType_Throws()
    {
        var untyped = Untyped(response: 42);

        var exception = Assert.Throws<KaleidoFrameworkException>(() =>
        {
            ProcessStepHandlerResult<Response> converted = untyped;
            return converted;
        });

        Assert.Equal(ProcessorErrorCodes.InvalidRequiredStep, exception.Code);
    }

    [Fact]
    public void Conversion_OfNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            ProcessStepHandlerResult<Response> converted = (ProcessStepHandlerResult)null!;
            return converted;
        });
    }

    private sealed record Response(string Value);

    private sealed record NextStep : IProcessStep;

    private sealed record AnswerStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }
        public string ExchangeId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
