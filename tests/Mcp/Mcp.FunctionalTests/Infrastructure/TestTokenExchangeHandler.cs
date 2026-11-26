namespace Kaleido.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The MCP host's token exchange stand-in (#574): user calls carry a newly signed token for
/// the validated caller, never their inbound credential; registration and hydration use an app token.
/// </summary>
internal sealed class TestTokenExchangeHandler(KaleidoServiceOptions service) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = KaleidoCallContext.Of(request);
        if (call.Purpose == KaleidoCallPurpose.ServiceOwn)
        {
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App($"{service.ServiceName}-app",
                ["registrant", "registry-reader", $"Kaleido.Registrant.{service.ServiceName}"]));
        }
        else if (call is { Caller: { Identity.IsAuthenticated: true } caller, CallerAuthorization: { } })
        {
            var name = caller.FindFirst(TestTokenIssuer.NameClaimType)?.Value
                ?? throw new InvalidOperationException("The validated caller has no user name to exchange.");
            var roles = caller.FindAll(TestTokenIssuer.RoleClaimType).Select(x => x.Value).ToArray();
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(name, roles, app: "mcp-exchange", tokenId: Guid.NewGuid().ToString("N")));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
