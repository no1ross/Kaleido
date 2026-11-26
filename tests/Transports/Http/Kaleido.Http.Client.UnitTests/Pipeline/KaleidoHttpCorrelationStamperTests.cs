namespace Kaleido.Http.Client.Pipeline.UnitTests;

public sealed class KaleidoHttpCorrelationStamperTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void Stamp_ForwardsEndToEndHeadersWithoutRepeatingInboundCallingStep()
    {
        var processId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gateway.test/registry");
        var context = new KaleidoCorrelationContext
        {
            RequestId = "request-1",
            Origin = "mcp\r\ninjected",
            OriginTool = "search",
            ProcessId = processId,
            CallingProcessorName = "other-service",
            CallingStepName = "other-step",
        };

        request.StampKaleidoCorrelation(context);

        Assert.Equal("request-1", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
        Assert.Equal("mcpinjected", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.Origin)));
        Assert.Equal("search", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.OriginTool)));
        Assert.Equal(processId.ToString("D"), Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public void Stamp_ExecutingStepUsesThisServicesPerHopIdentity_NotTheInboundCallers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/member/1/queryable/my-context/query");
        var context = new KaleidoCorrelationContext
        {
            RequestId = "request-1",
            CallingProcessorName = "inbound-service",
            CallingStepName = "inbound-step",
            ExecutingStepName = "current-step\r\ninjected",
        };

        request.StampKaleidoCorrelation(context, localServiceName: "reader-service\r\ninjected");

        Assert.Equal("reader-serviceinjected", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.CallingProcessor)));
        Assert.Equal("current-stepinjected", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.CallingStep)));
    }

    [Fact]
    public void Stamp_ExplicitProcessIdWinsOverTheInboundContext()
    {
        var inbound = Guid.NewGuid();
        var explicitId = Guid.NewGuid();
        var context = new KaleidoCorrelationContext { RequestId = "request-1", ProcessId = inbound };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/clinic/1/processes");

        request.StampKaleidoCorrelation(context, explicitId);

        Assert.Equal(explicitId.ToString("D"), Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        Assert.Equal("request-1", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
    }

    [Fact]
    public void Stamp_SanitizesARequestIdWithControlCharactersBeforeSending()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gateway.test/registry");
        request.StampKaleidoCorrelation(new KaleidoCorrelationContext { RequestId = "req\r\ninjected" });
        Assert.Equal("reqinjected", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
    }

    [Fact]
    public void Stamp_ExecutingStepWithoutLocalService_DoesNotReuseInboundCallingIdentity()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gateway.test/registry");
        var context = new KaleidoCorrelationContext
        {
            ExecutingStepName = "local-step",
            CallingProcessorName = "inbound-service",
            CallingStepName = "inbound-step",
        };

        request.StampKaleidoCorrelation(context);

        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public void Stamp_OmitsMissingOrSanitizedEmptyValues()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gateway.test/registry");
        var context = new KaleidoCorrelationContext { RequestId = "\r\n", Origin = "\r\n", OriginTool = " " };

        request.StampKaleidoCorrelation(context);

        Assert.Empty(request.Headers);
    }
}
