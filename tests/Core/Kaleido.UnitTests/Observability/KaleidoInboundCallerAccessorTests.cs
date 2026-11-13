using System.Security.Claims;
using Kaleido.UnitTests;

namespace Kaleido.Observability.UnitTests;

public sealed class KaleidoInboundCallerAccessorTests
    : SutFixture
{
    private static KaleidoInboundCallerAccessor CreateSut() => new();

    [Fact]
    public void BeforeTheTransportSetsIt_ThereIsNoInboundRequest()
    {
        var sut = CreateSut();

        Assert.False(sut.IsInboundRequest);
        Assert.Null(sut.Principal);
        Assert.Null(sut.Credential);
    }

    [Fact]
    public void Set_AnAuthenticatedCaller_KeepsItsPrincipalAndCredential()
    {
        var sut = CreateSut();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "carol")], "test"));

        sut.Set(principal, "Bearer carol");

        Assert.True(sut.IsInboundRequest);
        Assert.Same(principal, sut.Principal);
        Assert.Equal("Bearer carol", sut.Credential);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_AnAnonymousCaller_IsAnInboundRequestWithoutPrincipalOrCredential(string? credential)
    {
        var sut = CreateSut();

        sut.Set(new ClaimsPrincipal(new ClaimsIdentity()), credential);

        Assert.True(sut.IsInboundRequest);
        Assert.Null(sut.Principal);
        Assert.Null(sut.Credential);
    }
}
