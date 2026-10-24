using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Middleware.UnitTests;

public sealed class KaleidoExceptionHandlerTests
    : SutFixture
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly Guid ConflictedProcess = Guid.Parse("6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13");

    public KaleidoExceptionHandlerTests()
    {
        Logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private Mock<ILogger<KaleidoExceptionHandler>> Logger { get; } = new();

    private KaleidoExceptionHandler CreateSut() =>
        new(Logger.Object);

    public static TheoryData<string, LogLevel> LogLevelCases { get; } =
        new()
        {
            { nameof(KaleidoValidationException), LogLevel.Warning },
            { nameof(ArgumentException), LogLevel.Warning },
            { nameof(BadHttpRequestException), LogLevel.Warning },
            { nameof(System.Text.Json.JsonException), LogLevel.Warning },
            { nameof(KaleidoConcurrencyException), LogLevel.Warning },
            { nameof(KaleidoConfigurationException), LogLevel.Error },
            { nameof(KaleidoFrameworkException), LogLevel.Error },
            { nameof(Exception), LogLevel.Error },
        };

    [Theory]
    [MemberData(nameof(LogLevelCases))]
    public async Task HandleAsync_LogsOnceAtTheContractLevel(string exceptionName, LogLevel expectedLevel)
    {
        var exception = Create(exceptionName);

        await CreateSut().HandleAsync(CreateContext(), exception, CancellationToken.None);

        var log = Assert.Single(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(expectedLevel, log.Arguments[0]);
        Assert.False(string.IsNullOrWhiteSpace(log.Arguments[2]?.ToString()));
    }

    [Fact]
    public async Task HandleAsync_LogsTheValidationCodeAndMessage()
    {
        await CreateSut().HandleAsync(
            CreateContext(),
            new KaleidoValidationException("qry_invalid_field", "bad field"),
            CancellationToken.None);

        var log = Assert.Single(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal("Validation failed [qry_invalid_field]: bad field", log.Arguments[2]?.ToString());
    }

    [Fact]
    public async Task HandleAsync_LogsTheConflictedProcess()
    {
        await CreateSut().HandleAsync(
            CreateContext(),
            new KaleidoConcurrencyException(ConflictedProcess),
            CancellationToken.None);

        var log = Assert.Single(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(
            $"Process {ConflictedProcess} was changed by another request; nothing was saved (409).",
            log.Arguments[2]?.ToString());
    }

    [Fact]
    public async Task HandleAsync_WhenOperationCanceled_LogsAtDebugOnly()
    {
        await CreateSut().HandleAsync(CreateContext(), new OperationCanceledException(), CancellationToken.None);

        var log = Assert.Single(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, log.Arguments[0]);
        Assert.Equal("Request was canceled by the client.", log.Arguments[2]?.ToString());
    }

    [Fact]
    public async Task HandleAsync_WhenTheLoggerIsDisabled_StillWritesTheAnswer()
    {
        Logger.Reset();
        Logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(false);
        var context = CreateContext();

        await CreateSut().HandleAsync(context, new KaleidoValidationException("qry_invalid_field", "bad field"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.DoesNotContain(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_WhenBadHttpRequestCarriesAStatus_AnswersWithThatStatus()
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(
            context,
            new BadHttpRequestException("too large", StatusCodes.Status413PayloadTooLarge),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        var body = JsonSerializer.Deserialize<KaleidoErrorResponse>(ReadBody(context), WebJson);
        var error = Assert.Single(Assert.IsType<KaleidoErrorResponse>(body).Errors);
        Assert.Equal("argument_error", error.Code);
        Assert.Equal("too large", error.Message);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status403Forbidden)]
    [InlineData(false, StatusCodes.Status401Unauthorized)]
    public async Task HandleAsync_AuthorizationDenied_WithoutAuthentication_SetsTheStatusCode(
        bool callerIsAuthenticated,
        int expectedStatus)
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(
            context,
            new KaleidoAuthorizationException("denied", callerIsAuthenticated),
            CancellationToken.None);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
        var log = Assert.Single(Logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, log.Arguments[0]);
        Assert.StartsWith(
            $"Authorization denied (caller authenticated: {callerIsAuthenticated}): ",
            log.Arguments[2]?.ToString());
    }

    [Fact]
    public async Task HandleAsync_AuthorizationDenied_ForAuthenticatedCaller_Forbids()
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = CreateContext(authentication.Object);

        await CreateSut().HandleAsync(
            context,
            new KaleidoAuthorizationException("denied", callerIsAuthenticated: true),
            CancellationToken.None);

        authentication.Verify(x => x.ForbidAsync(context, null, null), Times.Once);
        authentication.Verify(x => x.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AuthorizationDenied_ForAnonymousCaller_Challenges()
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = CreateContext(authentication.Object);

        await CreateSut().HandleAsync(
            context,
            new KaleidoAuthorizationException("denied", callerIsAuthenticated: false),
            CancellationToken.None);

        authentication.Verify(x => x.ChallengeAsync(context, null, null), Times.Once);
        authentication.Verify(x => x.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()), Times.Never);
    }

    public static TheoryData<string, int, string, string> ErrorContractCases { get; } =
        new()
        {
            { nameof(KaleidoValidationException), StatusCodes.Status400BadRequest, "qry_invalid_field", "bad field" },
            { nameof(ArgumentException), StatusCodes.Status400BadRequest, "argument_error", "An invalid argument was provided." },
            { nameof(BadHttpRequestException), StatusCodes.Status400BadRequest, "argument_error", "bad request" },
            { nameof(System.Text.Json.JsonException), StatusCodes.Status400BadRequest, "argument_error", "The request body does not fit the expected shape." },
            { nameof(KaleidoConfigurationException), StatusCodes.Status500InternalServerError, "pro_missing_handler", "The service is not configured correctly." },
            { nameof(KaleidoFrameworkException), StatusCodes.Status500InternalServerError, "type_mismatch", "An internal error occurred." },
            { nameof(KaleidoConcurrencyException), StatusCodes.Status409Conflict, "pro_process_conflict", $"Process '{ConflictedProcess}' was changed by another request. Read its state again and decide on the next step." },
            { nameof(Exception), StatusCodes.Status500InternalServerError, "framework_error", "An unexpected error occurred." },
        };

    [Theory]
    [MemberData(nameof(ErrorContractCases))]
    public async Task HandleAsync_WritesTheExpectedContract(
        string exceptionName,
        int expectedStatus,
        string expectedCode,
        string expectedMessage)
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(context, Create(exceptionName), CancellationToken.None);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);

        var body = JsonSerializer.Deserialize<KaleidoErrorResponse>(ReadBody(context), WebJson);
        var error = Assert.Single(Assert.IsType<KaleidoErrorResponse>(body).Errors);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedMessage, error.Message);
        Assert.Null(error.Field);
    }

    // A 4xx is the caller's mistake: the request's span stays unset; only a 5xx marks it as an error.
    [Theory]
    [MemberData(nameof(ErrorContractCases))]
    public async Task HandleAsync_MarksTheSpanAsAnError_OnlyFor5xx(
        string exceptionName,
        int expectedStatus,
        string expectedCode,
        string expectedMessage)
    {
        _ = (expectedCode, expectedMessage);
        using var activity = new System.Diagnostics.Activity("request").Start();

        await CreateSut().HandleAsync(CreateContext(), Create(exceptionName), CancellationToken.None);

        Assert.Equal(
            expectedStatus >= 500 ? System.Diagnostics.ActivityStatusCode.Error : System.Diagnostics.ActivityStatusCode.Unset,
            activity.Status);
    }

    public static TheoryData<Exception, string> SensitiveFailures { get; } =
        new()
        {
            { new KaleidoConfigurationException("pro_missing_handler", "Server=db;Password=hunter2"), "pro_missing_handler" },
            { new KaleidoFrameworkException("type_mismatch", "Server=db;Password=hunter2"), "type_mismatch" },
            { new InvalidOperationException("Server=db;Password=hunter2"), nameof(InvalidOperationException) },
        };

    // #290: a 500 never carries the exception's text, neither in the body nor in the span status;
    // the code (or, for an unexpected failure, the exception type) identifies it, the log has the detail.
    [Theory]
    [MemberData(nameof(SensitiveFailures))]
    public async Task HandleAsync_A500_NeverCarriesTheExceptionText(Exception exception, string expectedStatusDescription)
    {
        using var activity = new System.Diagnostics.Activity("request").Start();
        var context = CreateContext();

        await CreateSut().HandleAsync(context, exception, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain("hunter2", ReadBody(context), StringComparison.Ordinal);
        Assert.Equal(expectedStatusDescription, activity.StatusDescription);
    }

    [Theory]
    [MemberData(nameof(ErrorContractCases))]
    public async Task HandleAsync_WhenTheClientIsGone_WritesNothing(
        string exceptionName,
        int expectedStatus,
        string expectedCode,
        string expectedMessage)
    {
        _ = (expectedStatus, expectedCode, expectedMessage);
        var context = CreateContext();

        await CreateSut().HandleAsync(context, Create(exceptionName), new CancellationToken(canceled: true));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    [Fact]
    public async Task HandleAsync_WhenTheClientIsGone_DoesNotChallengeOrForbid()
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(
            context,
            new KaleidoAuthorizationException("denied", callerIsAuthenticated: true),
            new CancellationToken(canceled: true));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenOperationCanceled_WritesNothing()
    {
        var context = CreateContext();

        await CreateSut().HandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    [Fact]
    public async Task HandleAsync_WhenTheResponseHasStarted_DoesNotWriteABody()
    {
        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        await CreateSut().HandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.Equal(string.Empty, ReadBody(context));
    }

    private static Exception Create(string exceptionName) =>
        exceptionName switch
        {
            nameof(KaleidoValidationException) => new KaleidoValidationException("qry_invalid_field", "bad field"),
            nameof(ArgumentException) => new ArgumentException("bad argument"),
            nameof(BadHttpRequestException) => new BadHttpRequestException("bad request"),
            nameof(System.Text.Json.JsonException) => new System.Text.Json.JsonException("The JSON value could not be converted."),
            nameof(KaleidoConfigurationException) => new KaleidoConfigurationException("pro_missing_handler", "no handler"),
            nameof(KaleidoFrameworkException) => new KaleidoFrameworkException("type_mismatch", "bad type"),
            nameof(KaleidoConcurrencyException) => new KaleidoConcurrencyException(ConflictedProcess),
            _ => new InvalidOperationException("boom"),
        };

    private static DefaultHttpContext CreateContext() =>
        new()
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream(),
            },
        };

    // #443: the error body is Kaleido's wire contract, so a host's own JSON settings (here PascalCase,
    // which WriteAsJsonAsync would otherwise use) never change it.
    [Theory]
    [InlineData(nameof(KaleidoValidationException))]
    [InlineData(nameof(BadHttpRequestException))]
    [InlineData(nameof(KaleidoConcurrencyException))]
    [InlineData(nameof(KaleidoFrameworkException))]
    [InlineData(nameof(Exception))]
    public async Task HandleAsync_WritesTheErrorBodyWithKaleidosJson_WhateverTheHostConfigures(string exceptionName)
    {
        var services = new ServiceCollection();
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = null);
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }),
            Response = { Body = new MemoryStream() },
        };

        await CreateSut().HandleAsync(context, Create(exceptionName), CancellationToken.None);

        using var body = JsonDocument.Parse(ReadBody(context));
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateArray());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));
    }

    private static DefaultHttpContext CreateContext(IAuthenticationService authentication)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authentication);

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }),
            Response =
            {
                Body = new MemoryStream(),
            },
        };
    }

    private static string ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    private sealed class StartedResponseFeature
        : IHttpResponseFeature
    {
        public int StatusCode { get; set; } =
            StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } =
            new HeaderDictionary();

        public Stream Body { get; set; } =
            new MemoryStream();

        public bool HasStarted =>
            true;

        public void OnStarting(
            Func<object, Task> callback,
            object state)
        {
        }

        public void OnCompleted(
            Func<object, Task> callback,
            object state)
        {
        }
    }
}
