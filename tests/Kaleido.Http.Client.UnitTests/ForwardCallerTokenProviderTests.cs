using System.Security.Claims;

namespace Kaleido.Http.Client.UnitTests;

public sealed class ForwardCallerTokenProviderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ForwardCallerTokenProvider CreateSut() => new();

    private static KaleidoCallCredentialRequest Request(KaleidoCallPurpose purpose, string? callerAuthorization) =>
        new()
        {
            ServiceName = "member",
            ClientName = "Member",
            Purpose = purpose,
            Caller = purpose == KaleidoCallPurpose.OnBehalfOfCaller ? new ClaimsPrincipal(new ClaimsIdentity([], "test")) : null,
            CallerAuthorization = callerAuthorization,
        };

    [Fact]
    public async Task ACallersCall_SendsTheCallersCredentialUnchanged()
    {
        var credential = await CreateSut().GetCredentialAsync(Request(KaleidoCallPurpose.OnBehalfOfCaller, "Bearer carol"), TestContext.Current.CancellationToken);

        Assert.Equal(KaleidoCallCredential.Header("Bearer carol"), credential);
    }

    // E.g. a cookie-authenticated caller: there is no token to forward, so the call is anonymous.
    [Fact]
    public async Task ACallerWithoutACredential_IsSentWithNone()
    {
        var credential = await CreateSut().GetCredentialAsync(Request(KaleidoCallPurpose.OnBehalfOfCaller, callerAuthorization: null), TestContext.Current.CancellationToken);

        Assert.Same(KaleidoCallCredential.None, credential);
    }

    [Fact]
    public async Task TheServicesOwnCall_KeepsTheHostsConfiguredCredential()
    {
        var credential = await CreateSut().GetCredentialAsync(Request(KaleidoCallPurpose.ServiceOwn, callerAuthorization: null), TestContext.Current.CancellationToken);

        Assert.Same(KaleidoCallCredential.Configured, credential);
    }
}
