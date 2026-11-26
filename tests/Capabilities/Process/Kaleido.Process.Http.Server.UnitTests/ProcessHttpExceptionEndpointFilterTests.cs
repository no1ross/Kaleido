using Kaleido.Http;
using Kaleido.Process.Exceptions;
using Kaleido.Process.Recovery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class ProcessHttpExceptionEndpointFilterTests : Kaleido.UnitTests.SutFixture
{
    private static ProcessHttpExceptionEndpointFilter CreateSut() => new();

    [Fact]
    public async Task ProcessConcurrency_Answers409WithoutChangingTheNeutralException()
    {
        var processId = Guid.NewGuid();
        var error = await AnswerAsync(new ProcessConcurrencyException(processId));

        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.ProcessConflict, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error.Value).Errors).Code);
    }

    [Theory]
    [InlineData("PendingRecovery", StatusCodes.Status503ServiceUnavailable, ProcessorErrorCodes.OutcomePendingRecovery)]
    [InlineData("Conflict", StatusCodes.Status409Conflict, ProcessorErrorCodes.OutcomeNotSaved)]
    [InlineData("NotSaved", StatusCodes.Status500InternalServerError, ProcessorErrorCodes.OutcomeNotSaved)]
    public async Task StepOutcomeNotSaved_DoesNotPretendItIsSafeToRetry(string outcomeName, int status, string code)
    {
        var error = await AnswerAsync(new StepOutcomeNotSavedException(Guid.NewGuid(), "Close", Enum.Parse<UnsavedOutcome>(outcomeName), new TimeoutException("store")));

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error.Value).Errors).Code);
    }

    [Fact]
    public async Task ProcessRecovering_Answers409WithoutRunningAnotherStep()
    {
        var error = await AnswerAsync(new ProcessRecoveringException(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(ProcessorErrorCodes.ProcessRecovering, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error.Value).Errors).Code);
    }

    private async Task<JsonHttpResult<KaleidoErrorResponse>> AnswerAsync(Exception exception) =>
        Assert.IsType<JsonHttpResult<KaleidoErrorResponse>>(await CreateSut().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()), _ => throw exception));
}
