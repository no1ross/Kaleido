using Kaleido.Exceptions;
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

    public static TheoryData<string, int, string> ErrorContractCases { get; } =
        new()
        {
            {
                nameof(KaleidoValidationException),
                StatusCodes.Status400BadRequest,
                "{\"errors\":[{\"code\":\"qry_invalid_field\",\"message\":\"bad field\",\"field\":null}]}"
            },
            {
                nameof(ArgumentException),
                StatusCodes.Status400BadRequest,
                "{\"errors\":[{\"code\":\"argument_error\",\"message\":\"An invalid argument was provided.\",\"field\":null}]}"
            },
            {
                nameof(BadHttpRequestException),
                StatusCodes.Status400BadRequest,
                "{\"errors\":[{\"code\":\"argument_error\",\"message\":\"bad request\",\"field\":null}]}"
            },
            {
                nameof(KaleidoConfigurationException),
                StatusCodes.Status500InternalServerError,
                "{\"errors\":[{\"code\":\"pro_missing_handler\",\"message\":\"no handler\",\"field\":null}]}"
            },
            {
                nameof(KaleidoFrameworkException),
                StatusCodes.Status500InternalServerError,
                "{\"errors\":[{\"code\":\"type_mismatch\",\"message\":\"bad type\",\"field\":null}]}"
            },
            {
                nameof(Exception),
                StatusCodes.Status500InternalServerError,
                "{\"errors\":[{\"code\":\"framework_error\",\"message\":\"An unexpected error occurred.\",\"field\":null}]}"
            }
        };

    [Theory]
    [MemberData(nameof(ErrorContractCases))]
    public async Task InvokeAsync_WhenExceptionIsThrown_ReturnsExpectedContract(
        string exceptionName,
        int expectedStatus,
        string expectedBody)
    {
        var context = CreateContext();

        Next = _ => throw exceptionName switch
        {
            nameof(KaleidoValidationException) =>
                new KaleidoValidationException("qry_invalid_field", "bad field"),
            nameof(ArgumentException) =>
                new ArgumentException("bad argument"),
            nameof(BadHttpRequestException) =>
                new BadHttpRequestException("bad request"),
            nameof(KaleidoConfigurationException) =>
                new KaleidoConfigurationException("pro_missing_handler", "no handler"),
            nameof(KaleidoFrameworkException) =>
                new KaleidoFrameworkException("type_mismatch", "bad type"),
            _ =>
                new InvalidOperationException("boom")
        };

        var middleware = CreateSut();

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal(expectedBody, ReadBody(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenOperationCanceled_PassesThroughQuietly()
    {
        var context = CreateContext();

        Next = _ => throw new OperationCanceledException();

        var middleware = CreateSut();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionAfterResponseStarted_DoesNotWriteBody()
    {
        var context = CreateContext();

        Next = async httpContext =>
        {
            await httpContext.Response.StartAsync();
            throw new InvalidOperationException("boom");
        };

        var middleware = CreateSut();

        await middleware.InvokeAsync(context);

        Assert.Equal(string.Empty, ReadBody(context));
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
