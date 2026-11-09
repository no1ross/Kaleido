using System.Security.Claims;
using Kaleido.Exceptions;

namespace Kaleido.Http.Client.Pipeline.UnitTests;

// #574: the innermost handler decides what a call made for a caller carries; the service's own calls
// keep what the host's handlers set.
public sealed class KaleidoOnBehalfOfGuardTests
    : Kaleido.UnitTests.SutFixture
{
    private const string Inbound = "Bearer carol-inbound";
    private const string HostSet = "Bearer set-by-the-host";

    private static readonly ClaimsPrincipal Carol = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "carol")], "test"));

    private static (HttpMessageInvoker Invoker, List<HttpRequestMessage> Sent) CreateSut(KaleidoOnBehalfOfCalls? mode)
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

        var options = mode is { } set ? new KaleidoClientOptions { OnBehalfOfCalls = set } : null;
        return (new HttpMessageInvoker(new KaleidoOnBehalfOfGuard(options) { InnerHandler = inner.Object }), sent);
    }

    // A request as the host's handlers left it (`authorization`), made for `caller` with `inboundToken`,
    // or the service's own when `forCaller` is false.
    private static HttpRequestMessage Request(bool forCaller, ClaimsPrincipal? caller = null, string? authorization = HostSet, string? inboundToken = Inbound)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://member.test/member/processes/1");

        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (forCaller)
        {
            request.Options.Set(KaleidoCallContext.Key, KaleidoCallContext.ForCaller(caller, inboundToken));
        }

        return request;
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string[] Authorization(HttpRequestMessage sent) =>
        sent.Headers.TryGetValues("Authorization", out var values) ? [.. values] : [];

    [Theory]
    [InlineData(null)]
    [InlineData(KaleidoOnBehalfOfCalls.Forward)]
    public async Task Forward_ACallersCall_CarriesTheInboundToken_WhateverTheHostSet(KaleidoOnBehalfOfCalls? mode)
    {
        var (invoker, sent) = CreateSut(mode);
        using var request = Request(forCaller: true, Carol);

        await invoker.SendAsync(request, Cancel);

        Assert.Equal([Inbound], Authorization(Assert.Single(sent)));
    }

    // An authenticated caller whose request carried no Authorization (e.g. a cookie): nothing is sent for it.
    [Fact]
    public async Task Forward_ACallerWithoutAnInboundToken_IsSentNothing()
    {
        var (invoker, sent) = CreateSut(KaleidoOnBehalfOfCalls.Forward);
        using var request = Request(forCaller: true, Carol, inboundToken: null);

        await invoker.SendAsync(request, Cancel);

        Assert.Empty(Authorization(Assert.Single(sent)));
    }

    [Theory]
    [InlineData(KaleidoOnBehalfOfCalls.Forward)]
    [InlineData(KaleidoOnBehalfOfCalls.Exchange)]
    public async Task AnAnonymousCallersCall_CarriesNothing_WhateverTheHostSet(KaleidoOnBehalfOfCalls mode)
    {
        var (invoker, sent) = CreateSut(mode);
        using var request = Request(forCaller: true, caller: null);

        await invoker.SendAsync(request, Cancel);

        Assert.Empty(Authorization(Assert.Single(sent)));
    }

    [Fact]
    public async Task Exchange_ACallersCall_CarriesTheTokenTheHostSet()
    {
        var (invoker, sent) = CreateSut(KaleidoOnBehalfOfCalls.Exchange);
        using var request = Request(forCaller: true, Carol, authorization: "Bearer carol-for-member");

        await invoker.SendAsync(request, Cancel);

        Assert.Equal(["Bearer carol-for-member"], Authorization(Assert.Single(sent)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Inbound)]
    public async Task Exchange_WithoutAnExchangedToken_IsRefused_AndNothingIsSent(string? authorization)
    {
        var (invoker, sent) = CreateSut(KaleidoOnBehalfOfCalls.Exchange);
        using var request = Request(forCaller: true, Carol, authorization);

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => invoker.SendAsync(request, Cancel));

        Assert.Equal(HttpClientErrorCodes.CallerTokenNotExchanged, exception.Code);
        Assert.DoesNotContain(Inbound, exception.Message, StringComparison.Ordinal);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(KaleidoOnBehalfOfCalls.Forward)]
    [InlineData(KaleidoOnBehalfOfCalls.Exchange)]
    public async Task TheServicesOwnCall_KeepsWhatTheHostSet(KaleidoOnBehalfOfCalls mode)
    {
        var (invoker, sent) = CreateSut(mode);
        using var request = Request(forCaller: false);

        await invoker.SendAsync(request, Cancel);

        Assert.Equal([HostSet], Authorization(Assert.Single(sent)));
    }
}
