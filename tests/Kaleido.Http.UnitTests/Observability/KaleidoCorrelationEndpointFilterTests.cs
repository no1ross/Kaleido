using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Observability.UnitTests;

public sealed class KaleidoCorrelationEndpointFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoCorrelationEndpointFilter CreateSut() => new();

    // Runs the filter around an endpoint that does nothing.
    private static async Task InvokeAsync(HttpContext httpContext) =>
        await CreateSut().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object?>(null));

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
        {
            context.Request.Headers[KaleidoCorrelationHeaders.RequestId] = requestId;
        }

        if (processId.HasValue)
        {
            context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = processId.Value.ToString();
        }

        if (callingProcessor is not null)
        {
            context.Request.Headers[KaleidoCorrelationHeaders.CallingProcessor] = callingProcessor;
        }

        if (callingStep is not null)
        {
            context.Request.Headers[KaleidoCorrelationHeaders.CallingStep] = callingStep;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenContextInitializerNotRegistered_DoesNotThrow()
    {
        var httpContext = CreateContext();

        await InvokeAsync(httpContext);
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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

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

        await InvokeAsync(httpContext);

        Assert.Equal("req-9", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        // this service's own instance id, never the value sent by the caller
        Assert.Equal(serviceOptions.InstanceId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessorInstanceId));
        Assert.Equal("intake", activity.GetTagItem(KaleidoTelemetryTags.CallingProcessor));
        Assert.Equal("Capture", activity.GetTagItem(KaleidoTelemetryTags.CallingStep));
        Assert.Equal(processId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessId));
    }

    [Fact]
    public async Task InvokeAsync_WhenActivityCurrentWithoutServiceOptions_TagsNoInstanceIdOrProcessId()
    {
        var httpContext = CreateContext();
        SetHeaders(httpContext, requestId: "req-3");

        using var activity = new Activity("kaleido-test").Start();

        await InvokeAsync(httpContext);

        Assert.Equal("req-3", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        Assert.Null(activity.GetTagItem(KaleidoTelemetryTags.ProcessorInstanceId));
        Assert.Null(activity.GetTagItem(KaleidoTelemetryTags.ProcessId));
    }

    // The filter runs inside the endpoint, after the host's authentication: it judges trust against
    // the principal the host set and never authenticates on its own (#295).
    [Fact]
    public async Task InvokeAsync_NeverAuthenticatesTheCallerItself()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var authentication = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddSingleton(initializer.Object)
            .AddSingleton(new Mock<IAuthenticationSchemeProvider>().Object)
            .AddSingleton(authentication.Object);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        await InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(It.Is<KaleidoCorrelationContext>(c => c.RequestId != "req-42" && c.CallingProcessorName == null)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenTrustPredicateChecksTheUser_SeesTheHostsAuthenticatedCaller()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            new KaleidoHttpOptions { TrustCorrelationIdentity = ctx => ctx.User.IsInRole("internal-service") },
            new Mock<IAuthenticationSchemeProvider>());
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "svc-intake"), new Claim(ClaimTypes.Role, "internal-service")],
            "test"));
        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        await InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(It.Is<KaleidoCorrelationContext>(c => c.RequestId == "req-42" && c.CallingProcessorName == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthWiredAndThePrincipalHasNoIdentity_IgnoresIdentityHeaders()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            schemeProvider: new Mock<IAuthenticationSchemeProvider>());
        httpContext.User = new ClaimsPrincipal();
        SetHeaders(httpContext, requestId: "req-42", callingProcessor: "intake");

        await InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(It.Is<KaleidoCorrelationContext>(c => c.RequestId != "req-42" && c.CallingProcessorName == null)),
            Times.Once);
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

        await InvokeAsync(httpContext);
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
