using Kaleido.Exceptions;
using Kaleido.Http.Middleware;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoExceptionEndpointFilterTests
    : SutFixture
{
    private Mock<IKaleidoExceptionHandler> Handler { get; } = new();

    private KaleidoExceptionEndpointFilter CreateSut(bool withHandler = true) =>
        new(withHandler ? Handler.Object : null);

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointSucceeds_ReturnsItsResult()
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        var expected = Results.Ok("value");

        var result = await CreateSut().InvokeAsync(context, _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
        Handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointThrows_AnswersThroughTheHandlerInsideTheEndpoint()
    {
        using var aborted = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext { RequestAborted = aborted.Token };
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var exception = new KaleidoValidationException("qry_paging_not_supported", "Paging is not supported for this query.");

        var result = await CreateSut().InvokeAsync(context, _ => throw exception);

        Assert.IsType<EmptyHttpResult>(result);
        Handler.Verify(x => x.HandleAsync(httpContext, exception, aborted.Token), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithoutAHandler_LetsTheExceptionPropagate()
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        var exception = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await CreateSut(withHandler: false).InvokeAsync(context, _ => throw exception));

        Assert.Same(exception, thrown);
    }
}
