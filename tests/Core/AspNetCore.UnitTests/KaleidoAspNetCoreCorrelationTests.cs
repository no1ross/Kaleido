using Kaleido.AspNetCore.Observability;
using Microsoft.AspNetCore.Http;

namespace Kaleido.AspNetCore.UnitTests;

public sealed class KaleidoAspNetCoreCorrelationTests
{
    [Fact]
    public void Create_WhenContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KaleidoAspNetCoreCorrelation.Create(null!));
    }

    [Fact]
    public void HeaderConstants_UseExpectedNames()
    {
        Assert.Equal("X-Kaleido-Request-Id", KaleidoAspNetCoreHeaders.RequestId);
        Assert.Equal("X-Kaleido-Process-Id", KaleidoAspNetCoreHeaders.ProcessId);
        Assert.Equal("X-Kaleido-Processor-Id", KaleidoAspNetCoreHeaders.ProcessorId);
        Assert.Equal("X-Kaleido-Processor-Instance-Id", KaleidoAspNetCoreHeaders.ProcessorInstanceId);
        Assert.Equal("X-Kaleido-Orchestrator-Id", KaleidoAspNetCoreHeaders.OrchestratorId);
        Assert.Equal("X-Kaleido-Orchestrator-Instance-Id", KaleidoAspNetCoreHeaders.OrchestratorInstanceId);
    }

    [Fact]
    public void Create_MapsHeadersToCorrelationContext()
    {
        var processId = Guid.NewGuid();
        var processorInstanceId = Guid.NewGuid();
        var orchestratorInstanceId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoAspNetCoreHeaders.RequestId] = "REQ-001";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessId] = processId.ToString();
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessorId] = "processor-a";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessorInstanceId] = processorInstanceId.ToString();
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorId] = "orchestrator-a";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorInstanceId] = orchestratorInstanceId.ToString();

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Equal("REQ-001", result.RequestId);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal("processor-a", result.ProcessorId);
        Assert.Equal(processorInstanceId, result.ProcessorInstanceId);
        Assert.Equal("orchestrator-a", result.OrchestratorId);
        Assert.Equal(orchestratorInstanceId, result.OrchestratorInstanceId);
    }

    [Fact]
    public void Create_WhenHeadersAreBlank_ReturnsNullValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoAspNetCoreHeaders.RequestId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessorId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessorInstanceId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorInstanceId] = " ";

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Null(result.RequestId);
        Assert.Null(result.ProcessId);
        Assert.Null(result.ProcessorId);
        Assert.Null(result.ProcessorInstanceId);
        Assert.Null(result.OrchestratorId);
        Assert.Null(result.OrchestratorInstanceId);
    }

    [Fact]
    public void Create_WhenGuidHeaderIsInvalid_ThrowsBadHttpRequestException()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessId] = "not-a-guid";

        var exception = Assert.Throws<BadHttpRequestException>(() =>
            KaleidoAspNetCoreCorrelation.Create(context));

        Assert.Contains(
            KaleidoAspNetCoreHeaders.ProcessId,
            exception.Message);
    }
}
