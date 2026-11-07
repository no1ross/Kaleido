namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// The authorization test capabilities hosted in <c>ZeroTrust</c> (#503): a capability without an
/// explicit rule (and without a service default) is not published, not mapped and denied in generic
/// execute.
/// </summary>
public sealed class ZeroTrustAuthorizationFixture()
    : AuthorizationAspNetCoreFixture(KaleidoAuthorizationMode.ZeroTrust);
