using System.Diagnostics;
using System.Security.Claims;
using Kaleido.Observability;
using Kaleido.Processor.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Middleware.UnitTests;

public sealed class ObservabilityMiddlewareTests
    : Kaleido.UnitTests.SutFixture
{
    private static ObservabilityMiddleware CreateSut(RequestDelegate? next = null) =>
        new(next ?? (_ => Task.CompletedTask));

    private static bool IsGuid(string value) =>
        Guid.TryParse(value, out _);

    private static DefaultHttpContext CreateContext(
        Mock<IKaleidoCorrelationContextInitializer>? initializer = null,
        KaleidoHttpOptions? httpOptions = null,
        Mock<IAuthenticationSchemeProvider>? schemeProvider = null,
        KaleidoServiceOptions? serviceOptions = null)
    {
        var services = new ServiceCollection();

        if (serviceOptions is not null)
        {
            services.AddSingleton(serviceOptions);
        }

        if (initializer is not null)
        {
            services.AddSingleton(initializer.Object);
        }

        if (httpOptions is not null)
        {
            services.AddSingleton(httpOptions);
        }

        if (schemeProvider is not null)
        {
            services.AddSingleton(schemeProvider.Object);
        }

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    private static void SetHeaders(
        HttpContext context,
        string? requestId = null,
        Guid? processId = null,
        string? callingProcessor = null,
        string? callingStep = null)
    {
        if (requestId is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.RequestId] = requestId;
        if (processId.HasValue)
            context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = processId.Value.ToString();
        if (callingProcessor is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor] = callingProcessor;
        if (callingStep is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.CallingStep] = callingStep;
    }

    [Fact]
    public async Task InvokeAsync_WhenContextInitializerNotRegistered_DoesNotThrow()
    {
        var sut = CreateSut();
        var httpContext = CreateContext();

        await sut.InvokeAsync(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationHeadersPresent_InitializesParsedContext()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(initializer);
        var processId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-42",
            processId: processId,
            callingProcessor: "intake",
            callingStep: "Capture");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.ProcessId == processId &&
                    c.CallingProcessorName == "intake" &&
                    c.CallingStepName == "Capture")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeaders_GeneratesRequestIdOnly()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(initializer);

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    !string.IsNullOrWhiteSpace(c.RequestId) &&
                    c.ProcessId == null &&
                    c.CallingProcessorName == null &&
                    c.CallingStepName == null)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenTrustPredicateDenies_IgnoresIdentityHeaders()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            new KaleidoHttpOptions
            {
                TrustCorrelationIdentity = _ => false
            });
        var processId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-42",
            processId: processId,
            callingProcessor: "intake",
            callingStep: "Capture");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId != "req-42" &&
                    IsGuid(c.RequestId) &&
                    c.ProcessId == processId &&
                    c.CallingProcessorName == null &&
                    c.CallingStepName == null)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenTrustPredicateAllows_HonorsHeaders()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            new KaleidoHttpOptions
            {
                TrustCorrelationIdentity = _ => true
            });

        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.CallingProcessorName == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoPredicateAndAuthWired_IgnoresIdentityForAnonymous()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            schemeProvider: new Mock<IAuthenticationSchemeProvider>());

        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId != "req-42" &&
                    c.CallingProcessorName == null)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoPredicateAndAuthWired_HonorsIdentityForAuthenticated()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            schemeProvider: new Mock<IAuthenticationSchemeProvider>());

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test"));

        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.CallingProcessorName == "intake")),
            Times.Once);
    }

    private static DefaultHttpContext CreateContextAuthenticatedByDefaultScheme(
        Mock<IKaleidoCorrelationContextInitializer> initializer,
        bool authenticates,
        KaleidoHttpOptions? httpOptions = null)
    {
        var schemeProvider = new Mock<IAuthenticationSchemeProvider>();
        var scheme = new AuthenticationScheme("test", null, typeof(IAuthenticationHandler));

        schemeProvider
            .Setup(x => x.GetDefaultAuthenticateSchemeAsync())
            .ReturnsAsync(scheme);

        var authentication = new Mock<IAuthenticationService>();

        authentication
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), null))
            .ReturnsAsync(authenticates
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "svc-intake"), new Claim(ClaimTypes.Role, "internal-service")],
                        "test")),
                    "test"))
                : AuthenticateResult.NoResult());

        var services = new ServiceCollection()
            .AddSingleton(initializer.Object)
            .AddSingleton(schemeProvider.Object)
            .AddSingleton(authentication.Object);

        if (httpOptions is not null)
        {
            services.AddSingleton(httpOptions);
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        return httpContext;
    }

    // Kaleido's middleware runs ahead of the host's UseAuthentication(), so the caller is not
    // authenticated yet: the middleware authenticates it with the default scheme itself.
    [Theory]
    [InlineData(true, "req-42", "intake")]
    [InlineData(false, null, null)]
    public async Task InvokeAsync_WhenNotYetAuthenticated_TrustsIdentityOnlyIfTheDefaultSchemeAuthenticates(
        bool authenticates,
        string? expectedRequestId,
        string? expectedCallingProcessor)
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContextAuthenticatedByDefaultScheme(initializer, authenticates);

        await CreateSut().InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    (expectedRequestId == null ? c.RequestId != "req-42" : c.RequestId == expectedRequestId) &&
                    c.CallingProcessorName == expectedCallingProcessor)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenTrustPredicateChecksTheUser_SeesTheAuthenticatedCaller()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContextAuthenticatedByDefaultScheme(
            initializer,
            authenticates: true,
            new KaleidoHttpOptions { TrustCorrelationIdentity = ctx => ctx.User.IsInRole("internal-service") });

        await CreateSut().InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.CallingProcessorName == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenActivityCurrent_TagsCorrelationFields()
    {
        var serviceOptions = new KaleidoServiceOptions { ServiceName = "radiology" };
        var httpContext = CreateContext(serviceOptions: serviceOptions);
        var processId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-9",
            processId: processId,
            callingProcessor: "intake",
            callingStep: "Capture");
        httpContext.Request.Headers["X-Kaleido-Processor-Instance-Id"] = Guid.NewGuid().ToString();

        using var activity = new Activity("kaleido-test").Start();

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        Assert.Equal("req-9", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        // this service's own instance id, never the value sent by the caller
        Assert.Equal(serviceOptions.InstanceId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessorInstanceId));
        Assert.Equal("intake", activity.GetTagItem(KaleidoTelemetryTags.CallingProcessor));
        Assert.Equal("Capture", activity.GetTagItem(KaleidoTelemetryTags.CallingStep));
        Assert.Equal(processId.ToString(), activity.GetTagItem(ProcessorTelemetry.TagProcessId));
    }

    [Fact]
    public async Task InvokeAsync_EchoesOnlyRequestIdAndProcessIdOnResponse()
    {
        var httpContext = CreateContext();
        var responseFeature = new StartableResponseFeature();
        httpContext.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(responseFeature);
        var processId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-7",
            processId: processId,
            callingProcessor: "intake",
            callingStep: "Capture");

        await CreateSut().InvokeAsync(httpContext);
        await responseFeature.StartAsync();

        Assert.Equal("req-7", responseFeature.Headers[KaleidoCorrelationHeaders.RequestId].ToString());
        Assert.Equal(processId.ToString(), responseFeature.Headers[KaleidoCorrelationHeaders.ProcessId].ToString());
        Assert.False(responseFeature.Headers.ContainsKey(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(responseFeature.Headers.ContainsKey(KaleidoCorrelationHeaders.CallingStep));
        Assert.False(responseFeature.Headers.ContainsKey("X-Kaleido-Processor-Instance-Id"));
    }

    private sealed class StartableResponseFeature
        : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task StartAsync()
        {
            HasStarted = true;

            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }
        }
    }
}
