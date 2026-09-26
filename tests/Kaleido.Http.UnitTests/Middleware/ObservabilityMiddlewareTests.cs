using System.Diagnostics;
using Kaleido.Observability;
using Kaleido.Process.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests.Middleware;

public sealed class ObservabilityMiddlewareTests
{
    private static ObservabilityMiddleware CreateSut(RequestDelegate? next = null) =>
        new(next ?? (_ => Task.CompletedTask));

    private static DefaultHttpContext CreateContext(
        Mock<IKaleidoCorrelationContextInitializer>? initializer = null)
    {
        var services = new ServiceCollection();

        if (initializer is not null)
        {
            services.AddSingleton(initializer.Object);
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
        Assert.Equal(processId.ToString(), activity.GetTagItem(ProcessTelemetry.TagProcessId));
        Assert.Equal("Capture", activity.GetTagItem(ProcessTelemetry.TagStepName));
    }
}
