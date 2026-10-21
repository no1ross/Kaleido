using System.Security.Claims;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoCallContextTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoCallContext CreateSut() =>
        CreateSut(KaleidoCallPurpose.OnBehalfOfCaller, new ClaimsPrincipal(new ClaimsIdentity([], "test")), "Bearer carol");

    private static KaleidoCallContext CreateSut(KaleidoCallPurpose purpose, ClaimsPrincipal? caller, string? authorization) =>
        new(purpose, caller, authorization);

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

    // A registry fetch made while handling a caller's request is still the service's own call.
    [Fact]
    public void MarkServiceOwn_ReplacesACallersContext()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://member.test/");
        request.Options.Set(KaleidoCallContext.Key, CreateSut());

        KaleidoCallContext.MarkServiceOwn(request);

        Assert.Equal(CreateSut(KaleidoCallPurpose.ServiceOwn, null, null), KaleidoCallContext.Of(request));
    }
}
