using System.Diagnostics;
using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Server.Middleware.UnitTests;

public sealed class KaleidoExceptionHandlerTests : SutFixture
{
    private readonly Mock<ILogger<KaleidoExceptionHandler>> _logger = new();

    public KaleidoExceptionHandlerTests()
    {
        _logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private KaleidoExceptionHandler CreateSut() => new(_logger.Object);

    public static TheoryData<Exception, int, string, string> ErrorCases { get; } =
        new()
        {
            { new KaleidoValidationException("invalid_field", "bad field"), StatusCodes.Status400BadRequest, "invalid_field", "bad field" },
            { new ArgumentException("bad argument"), StatusCodes.Status400BadRequest, "argument_error", "An invalid argument was provided." },
            { new BadHttpRequestException("bad request"), StatusCodes.Status400BadRequest, "argument_error", "bad request" },
            { new JsonException("bad JSON"), StatusCodes.Status400BadRequest, "argument_error", "The request body does not fit the expected shape." },
            { new KaleidoConfigurationException("invalid_config", "secret"), StatusCodes.Status500InternalServerError, "invalid_config", "The service is not configured correctly." },
            { new KaleidoFrameworkException("framework_failure", "secret"), StatusCodes.Status500InternalServerError, "framework_failure", "An internal error occurred." },
            { new InvalidOperationException("secret"), StatusCodes.Status500InternalServerError, "framework_error", "An unexpected error occurred." },
        };

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task HandleAsync_WritesTheExpectedContract(Exception exception, int status, string code, string message)
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(context, exception, CancellationToken.None);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        var response = JsonSerializer.Deserialize<KaleidoErrorResponse>(ReadBody(context), KaleidoJsonOptions.Options);
        var error = Assert.Single(Assert.IsType<KaleidoErrorResponse>(response).Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Field);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task HandleAsync_MarksTheSpanAsAnError_OnlyFor5xx(Exception exception, int status, string code, string message)
    {
        _ = (code, message);
        using var activity = new Activity("request").Start();

        await CreateSut().HandleAsync(CreateContext(), exception, CancellationToken.None);

        Assert.Equal(status >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task HandleAsync_LogsValidationCodeAndMessage()
    {
        await CreateSut().HandleAsync(CreateContext(), new KaleidoValidationException("invalid_field", "bad field"), CancellationToken.None);

        var log = Assert.Single(_logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, log.Arguments[0]);
        Assert.Equal("Validation failed [invalid_field]: bad field", log.Arguments[2]?.ToString());
    }

    [Theory]
    [InlineData(true, StatusCodes.Status403Forbidden)]
    [InlineData(false, StatusCodes.Status401Unauthorized)]
    public async Task HandleAsync_AuthorizationDenied_WithoutAuthentication_SetsTheStatusCode(bool authenticated, int status)
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(context, new KaleidoAuthorizationException("denied", authenticated), CancellationToken.None);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_AuthorizationDenied_WithAuthentication_DelegatesToHost(bool authenticated)
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = CreateContext(authentication.Object);

        await CreateSut().HandleAsync(context, new KaleidoAuthorizationException("denied", authenticated), CancellationToken.None);

        authentication.Verify(x => x.ForbidAsync(context, null, null), authenticated ? Times.Once() : Times.Never());
        authentication.Verify(x => x.ChallengeAsync(context, null, null), authenticated ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task HandleAsync_CanceledRequest_LogsAtDebugAndWritesNothing()
    {
        var context = CreateContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        await CreateSut().HandleAsync(context, new OperationCanceledException(context.RequestAborted), context.RequestAborted);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
        var log = Assert.Single(_logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, log.Arguments[0]);
    }

    [Fact]
    public async Task HandleAsync_NonClientCancellation_IsA500()
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(context, new TaskCanceledException("timeout"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_A500_NeverExposesItsExceptionMessage()
    {
        using var activity = new Activity("request").Start();
        var context = CreateContext();

        await CreateSut().HandleAsync(context, new KaleidoConfigurationException("invalid_config", "Server=db;Password=private"), CancellationToken.None);

        Assert.DoesNotContain("private", ReadBody(context), StringComparison.Ordinal);
        Assert.Equal("invalid_config", activity.StatusDescription);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task HandleAsync_WhenClientIsGone_WritesNothing(Exception exception, int status, string code, string message)
    {
        _ = (status, code, message);
        var context = CreateContext();

        await CreateSut().HandleAsync(context, exception, new CancellationToken(canceled: true));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    private static DefaultHttpContext CreateContext(IAuthenticationService? authentication = null)
    {
        var services = new ServiceCollection();
        if (authentication is not null)
        {
            services.AddSingleton(authentication);
        }

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
