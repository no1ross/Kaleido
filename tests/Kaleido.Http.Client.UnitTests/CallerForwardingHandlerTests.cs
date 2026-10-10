namespace Kaleido.Http.Client.UnitTests;

public sealed class CallerForwardingHandlerTests
    : Kaleido.UnitTests.SutFixture
{
    private static (HttpMessageInvoker Invoker, List<HttpRequestMessage> Sent) CreateSut(
        bool forwarding,
        string? callerAuthorization)
    {
        var scope = new Mock<IKaleidoCallerForwarding>();
        scope.Setup(x => x.IsForwarding).Returns(forwarding);
        scope.Setup(x => x.CallerAuthorization).Returns(callerAuthorization);

        var sent = new List<HttpRequestMessage>();
        var inner = new Mock<HttpMessageHandler>();
        inner.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                sent.Add(request);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        return (new HttpMessageInvoker(new CallerForwardingHandler(scope.Object) { InnerHandler = inner.Object }), sent);
    }

    private static HttpRequestMessage WithServiceToken()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://intake.test/intake/processes/steps/x");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer svc-router");
        return request;
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forwarding_ReplacesTheHostsCredentials_WithTheCallers()
    {
        var (invoker, sent) = CreateSut(forwarding: true, "Bearer carol");
        using var request = WithServiceToken();

        await invoker.SendAsync(request, Cancel);

        Assert.Equal(["Bearer carol"], Assert.Single(sent).Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task Forwarding_AnAnonymousCaller_SendsNoCredentials()
    {
        var (invoker, sent) = CreateSut(forwarding: true, callerAuthorization: null);
        using var request = WithServiceToken();

        await invoker.SendAsync(request, Cancel);

        Assert.False(Assert.Single(sent).Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task NotForwarding_LeavesTheHostsCredentials()
    {
        var (invoker, sent) = CreateSut(forwarding: false, "Bearer carol");
        using var request = WithServiceToken();

        await invoker.SendAsync(request, Cancel);

        Assert.Equal(["Bearer svc-router"], Assert.Single(sent).Headers.GetValues("Authorization"));
    }
}
