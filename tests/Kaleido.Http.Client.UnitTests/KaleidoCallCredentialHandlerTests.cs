using System.Security.Claims;
using Kaleido.Exceptions;

namespace Kaleido.Http.Client.UnitTests;

// ADR 0022: the innermost handler sends exactly the provider's credential, and a call made for a
// caller never goes out with the host's own credentials.
public sealed class KaleidoCallCredentialHandlerTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ClaimsPrincipal Carol = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "carol")], "test"));

    private static (HttpMessageInvoker Invoker, List<HttpRequestMessage> Sent) CreateSut(IKaleidoCallCredentialProvider provider)
    {
        var sent = new List<HttpRequestMessage>();
        var inner = new Mock<HttpMessageHandler>();
        inner.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                sent.Add(request);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        return (new HttpMessageInvoker(new KaleidoCallCredentialHandler(provider, "Member", "member") { InnerHandler = inner.Object }), sent);
    }

    private static Mock<IKaleidoCallCredentialProvider> Answering(KaleidoCallCredential credential)
    {
        var provider = new Mock<IKaleidoCallCredentialProvider>();
        provider.Setup(x => x.GetCredentialAsync(It.IsAny<KaleidoCallCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);
        return provider;
    }

    // A request carrying the host's configured service token, made for `caller` (or the service's own when null).
    private static HttpRequestMessage Request(KaleidoCallPurpose purpose, ClaimsPrincipal? caller = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://member.test/member/registry");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer svc-host");

        if (purpose == KaleidoCallPurpose.OnBehalfOfCaller)
        {
            request.Options.Set(KaleidoCallContext.Key, new KaleidoCallContext(purpose, caller, "Bearer carol-inbound"));
        }

        return request;
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACallersCall_SendsTheProvidersCredential_AndAsksWithTheDestinationAndTheCaller()
    {
        var provider = Answering(KaleidoCallCredential.Header("Bearer carol-for-member"));
        var (invoker, sent) = CreateSut(provider.Object);
        using var request = Request(KaleidoCallPurpose.OnBehalfOfCaller, Carol);

        await invoker.SendAsync(request, Cancel);

        Assert.Equal(["Bearer carol-for-member"], Assert.Single(sent).Headers.GetValues("Authorization"));
        provider.Verify(x => x.GetCredentialAsync(
            It.Is<KaleidoCallCredentialRequest>(r =>
                r.ServiceName == "member" && r.ClientName == "Member" && r.Purpose == KaleidoCallPurpose.OnBehalfOfCaller &&
                r.Caller == Carol && r.CallerAuthorization == "Bearer carol-inbound"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnAnonymousCallersCall_SendsNoCredential_WithoutAskingTheProvider()
    {
        var provider = Answering(KaleidoCallCredential.Header("Bearer must-not-be-sent"));
        var (invoker, sent) = CreateSut(provider.Object);
        using var request = Request(KaleidoCallPurpose.OnBehalfOfCaller, caller: null);

        await invoker.SendAsync(request, Cancel);

        Assert.False(Assert.Single(sent).Headers.Contains("Authorization"));
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfiguredForACallersCall_IsRefused_AndNothingIsSent()
    {
        var (invoker, sent) = CreateSut(Answering(KaleidoCallCredential.Configured).Object);
        using var request = Request(KaleidoCallPurpose.OnBehalfOfCaller, Carol);

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => invoker.SendAsync(request, Cancel));

        Assert.Equal(HttpClientErrorCodes.ConfiguredCredentialForCaller, exception.Code);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ConfiguredForTheServicesOwnCall_KeepsTheHostsCredential()
    {
        var provider = Answering(KaleidoCallCredential.Configured);
        var (invoker, sent) = CreateSut(provider.Object);
        using var request = Request(KaleidoCallPurpose.ServiceOwn);

        await invoker.SendAsync(request, Cancel);

        Assert.Equal(["Bearer svc-host"], Assert.Single(sent).Headers.GetValues("Authorization"));
        provider.Verify(x => x.GetCredentialAsync(It.Is<KaleidoCallCredentialRequest>(r => r.Purpose == KaleidoCallPurpose.ServiceOwn && r.Caller == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task None_SendsNoCredential()
    {
        var (invoker, sent) = CreateSut(Answering(KaleidoCallCredential.None).Object);
        using var request = Request(KaleidoCallPurpose.ServiceOwn);

        await invoker.SendAsync(request, Cancel);

        Assert.False(Assert.Single(sent).Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task AProviderFailure_FailsTheCall_WithoutFallingBackToTheHostsCredential()
    {
        var provider = new Mock<IKaleidoCallCredentialProvider>();
        provider.Setup(x => x.GetCredentialAsync(It.IsAny<KaleidoCallCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("token exchange failed"));
        var (invoker, sent) = CreateSut(provider.Object);
        using var request = Request(KaleidoCallPurpose.OnBehalfOfCaller, Carol);

        await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(request, Cancel));

        Assert.Empty(sent);
    }
}
