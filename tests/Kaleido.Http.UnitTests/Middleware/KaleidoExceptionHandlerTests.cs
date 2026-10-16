using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.UnitTests;
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

    private Mock<ILogger<KaleidoExceptionHandler>> Logger { get; } = new();

    private KaleidoExceptionHandler CreateSut() =>
        new(Logger.Object);

    public static TheoryData<string, int, string, string> ErrorContractCases { get; } =
        new()
        {
            { nameof(KaleidoValidationException), StatusCodes.Status400BadRequest, "qry_invalid_field", "bad field" },
            { nameof(ArgumentException), StatusCodes.Status400BadRequest, "argument_error", "An invalid argument was provided." },
            { nameof(BadHttpRequestException), StatusCodes.Status400BadRequest, "argument_error", "bad request" },
            { nameof(System.Text.Json.JsonException), StatusCodes.Status400BadRequest, "argument_error", "The request body does not fit the expected shape." },
            { nameof(KaleidoConfigurationException), StatusCodes.Status500InternalServerError, "pro_missing_handler", "no handler" },
            { nameof(KaleidoFrameworkException), StatusCodes.Status500InternalServerError, "type_mismatch", "bad type" },
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
