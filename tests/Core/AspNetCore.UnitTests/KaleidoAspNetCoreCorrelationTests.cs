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
        Assert.Equal("X-Kaleido-Participant-Id", KaleidoAspNetCoreHeaders.ParticipantId);
        Assert.Equal("X-Kaleido-Participant-Instance-Id", KaleidoAspNetCoreHeaders.ParticipantInstanceId);
        Assert.Equal("X-Kaleido-Orchestrator-Id", KaleidoAspNetCoreHeaders.OrchestratorId);
        Assert.Equal("X-Kaleido-Orchestrator-Instance-Id", KaleidoAspNetCoreHeaders.OrchestratorInstanceId);
    }

    [Fact]
    public void Create_MapsHeadersToCorrelationContext()
    {
        var processId = Guid.NewGuid();
        var participantInstanceId = Guid.NewGuid();
        var orchestratorInstanceId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoAspNetCoreHeaders.RequestId] = "REQ-001";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessId] = processId.ToString();
        context.Request.Headers[KaleidoAspNetCoreHeaders.ParticipantId] = "participant-a";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ParticipantInstanceId] = participantInstanceId.ToString();
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorId] = "orchestrator-a";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorInstanceId] = orchestratorInstanceId.ToString();

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Equal("REQ-001", result.RequestId);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal("participant-a", result.ParticipantId);
        Assert.Equal(participantInstanceId, result.ParticipantInstanceId);
        Assert.Equal("orchestrator-a", result.OrchestratorId);
        Assert.Equal(orchestratorInstanceId, result.OrchestratorInstanceId);
    }

    [Fact]
    public void Create_WhenHeadersAreBlank_ReturnsNullValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[KaleidoAspNetCoreHeaders.RequestId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ProcessId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ParticipantId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.ParticipantInstanceId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorId] = " ";
        context.Request.Headers[KaleidoAspNetCoreHeaders.OrchestratorInstanceId] = " ";

        var result = KaleidoAspNetCoreCorrelation.Create(context);

        Assert.Null(result.RequestId);
        Assert.Null(result.ProcessId);
        Assert.Null(result.ParticipantId);
        Assert.Null(result.ParticipantInstanceId);
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
