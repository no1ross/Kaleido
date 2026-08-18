using Kaleido.AspNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Kaleido.AspNetCore.UnitTests;

public sealed class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_PassesThrough()
    {
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var context = CreateContext();
        var wasCalled = false;

        var middleware =
            new ExceptionMiddleware(
                next: httpContext =>
                {
                    wasCalled = true;
                    httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                    return Task.CompletedTask;
                },
                logger.Object);

        await middleware.InvokeAsync(context);

        Assert.True(wasCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentExceptionIsThrown_ReturnsBadRequestPayload()
    {
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var context = CreateContext();
        var middleware =
            new ExceptionMiddleware(
                _ => throw new ArgumentException("bad argument"),
                logger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("{\"message\":\"bad argument\"}", ReadBody(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidOperationExceptionIsThrown_ReturnsBadRequestPayload()
    {
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var context = CreateContext();
        var middleware =
            new ExceptionMiddleware(
                _ => throw new InvalidOperationException("bad operation"),
                logger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("{\"message\":\"bad operation\"}", ReadBody(context));
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static string ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }
}
