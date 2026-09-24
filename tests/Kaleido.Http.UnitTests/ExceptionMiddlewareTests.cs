using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.UnitTests;

internal sealed class ExceptionMiddlewareTests
    : SutFixture<ExceptionMiddleware>
{
    private Mock<ILogger<ExceptionMiddleware>> Logger { get; } = new();

    private RequestDelegate Next { get; set; } = _ => Task.CompletedTask;

    protected override ExceptionMiddleware CreateSut() =>
        new(Next, Logger.Object);

    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_PassesThrough()
    {
        var context = CreateContext();
        var wasCalled = false;

        Next = httpContext =>
        {
            wasCalled = true;
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        var middleware = CreateSut();

        await middleware.InvokeAsync(context);

        Assert.True(wasCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentExceptionIsThrown_ReturnsBadRequestPayload()
    {
        var context = CreateContext();

        Next = _ => throw new ArgumentException("bad argument");

        var middleware = CreateSut();

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
