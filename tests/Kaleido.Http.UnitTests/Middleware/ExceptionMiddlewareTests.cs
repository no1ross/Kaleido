using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Middleware.UnitTests;

public sealed class ExceptionMiddlewareTests
    : SutFixture
{
    private Mock<IKaleidoExceptionHandler> Handler { get; } = new();

    private RequestDelegate Next { get; set; } = _ => Task.CompletedTask;

    private ExceptionMiddleware CreateSut() =>
        new(Next, Handler.Object);

    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_PassesThrough()
    {
        var context = new DefaultHttpContext();
        var wasCalled = false;

        Next = httpContext =>
        {
            wasCalled = true;
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        await CreateSut().InvokeAsync(context);

        Assert.True(wasCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_HandsTheExceptionToTheHandlerWithTheRequestCancellation()
    {
        using var aborted = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        var exception = new InvalidOperationException("boom");

        Next = _ => throw exception;

        await CreateSut().InvokeAsync(context);

        Handler.Verify(x => x.HandleAsync(context, exception, aborted.Token), Times.Once);
    }
}
