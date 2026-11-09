using System.Security.Claims;

namespace Kaleido.Client.Calls.UnitTests;

public sealed class KaleidoCallContextTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly ClaimsPrincipal Carol = new(new ClaimsIdentity([], "test"));

    private static KaleidoCallContext CreateSut() =>
        KaleidoCallContext.ForCaller(Carol, "Bearer carol");

    [Fact]
    public void Of_ARequestWithoutAContext_IsTheServicesOwnCall()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://member.test/");

        var context = KaleidoCallContext.Of(request);

        Assert.Equal(KaleidoCallPurpose.ServiceOwn, context.Purpose);
        Assert.Null(context.Caller);
        Assert.Null(context.CallerAuthorization);
    }

    [Fact]
    public void Of_ARequestWithAContext_ReturnsIt()
    {
        var sut = CreateSut();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://member.test/");
        request.Options.Set(KaleidoCallContext.Key, sut);

        Assert.Same(sut, KaleidoCallContext.Of(request));
    }

    [Fact]
    public void ForCaller_IsACallForThatCaller_WithTheirToken()
    {
        var sut = CreateSut();

        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, sut.Purpose);
        Assert.Same(Carol, sut.Caller);
        Assert.Equal("Bearer carol", sut.CallerAuthorization);
    }

    // A registry fetch made while handling a caller's request is still the service's own call.
    [Fact]
    public void MarkServiceOwn_ReplacesACallersContext()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://member.test/");
        request.Options.Set(KaleidoCallContext.Key, CreateSut());

        KaleidoCallContext.MarkServiceOwn(request);

        var context = KaleidoCallContext.Of(request);
        Assert.Equal(KaleidoCallPurpose.ServiceOwn, context.Purpose);
        Assert.Null(context.Caller);
        Assert.Null(context.CallerAuthorization);
    }

    [Fact]
    public void Of_ANullRequest_Throws() =>
        Assert.Throws<ArgumentNullException>(() => KaleidoCallContext.Of(null!));
}
