using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.UnitTests;

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
        Assert.Equal(
            "{\"errors\":[{\"code\":\"argument_error\",\"message\":\"An invalid argument was provided.\",\"field\":null}]}",
            ReadBody(context));
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = serviceProvider,
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
