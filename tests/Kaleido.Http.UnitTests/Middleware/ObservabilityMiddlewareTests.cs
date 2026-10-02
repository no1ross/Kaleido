using System.Diagnostics;
using System.Security.Claims;
using Kaleido.Observability;
using Microsoft.AspNetCore.Authentication;
using Kaleido.Processor.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests.Middleware;

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
        Mock<IAuthenticationSchemeProvider>? schemeProvider = null)
    {
        var services = new ServiceCollection();

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
        Guid? processorInstanceId = null,
        string? sourceProcessor = null,
        string? stepName = null)
    {
        if (requestId is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.RequestId] = requestId;
        if (processId.HasValue)
            context.Request.Headers[KaleidoCorrelationHeaders.ProcessId] = processId.Value.ToString();
        if (processorInstanceId.HasValue)
            context.Request.Headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = processorInstanceId.Value.ToString();
        if (sourceProcessor is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.SourceProcessor] = sourceProcessor;
        if (stepName is not null)
            context.Request.Headers[KaleidoCorrelationHeaders.StepName] = stepName;
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
        var instanceId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-42",
            processId: processId,
            processorInstanceId: instanceId,
            sourceProcessor: "intake",
            stepName: "Capture");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.ProcessId == processId &&
                    c.ProcessorInstanceId == instanceId &&
                    c.SourceProcessorName == "intake" &&
                    c.StepName == "Capture")),
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
                    c.ProcessorInstanceId == null &&
                    c.SourceProcessorName == null &&
                    c.StepName == null)),
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
            processorInstanceId: Guid.NewGuid(),
            sourceProcessor: "intake",
            stepName: "Capture");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId != "req-42" &&
                    IsGuid(c.RequestId) &&
                    c.ProcessId == processId &&
                    c.ProcessorInstanceId == null &&
                    c.SourceProcessorName == null &&
                    c.StepName == null)),
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

        SetHeaders(httpContext, requestId: "req-42", sourceProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.SourceProcessorName == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoPredicateAndAuthWired_IgnoresIdentityForAnonymous()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var httpContext = CreateContext(
            initializer,
            schemeProvider: new Mock<IAuthenticationSchemeProvider>());

        SetHeaders(httpContext, requestId: "req-42", sourceProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId != "req-42" &&
                    c.SourceProcessorName == null)),
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

        SetHeaders(httpContext, requestId: "req-42", sourceProcessor: "intake");

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-42" &&
                    c.SourceProcessorName == "intake")),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenActivityCurrent_TagsCorrelationFields()
    {
        var httpContext = CreateContext();
        var processId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        SetHeaders(
            httpContext,
            requestId: "req-9",
            processId: processId,
            processorInstanceId: instanceId,
            sourceProcessor: "intake",
            stepName: "Capture");

        using var activity = new Activity("kaleido-test").Start();

        var sut = CreateSut();

        await sut.InvokeAsync(httpContext);

        Assert.Equal("req-9", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        Assert.Equal(instanceId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessorInstanceId));
        Assert.Equal("intake", activity.GetTagItem(KaleidoTelemetryTags.SourceProcessor));
        Assert.Equal(processId.ToString(), activity.GetTagItem(ProcessorTelemetry.TagProcessId));
        Assert.Equal("Capture", activity.GetTagItem(ProcessorTelemetry.TagStepName));
    }
}
