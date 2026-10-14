namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host set up for sign-in from MCP clients (#212): its own scheme validates tokens, the
/// MCP SDK's scheme answers the challenge with the protected-resource metadata, and the MCP endpoint
/// requires an authenticated caller.
/// </summary>
public sealed class OAuthSignInFixture : McpClinicFixture
{
    public OAuthSignInFixture()
        : base(withDownstreamThatIsDown: false, withSignIn: true)
    {
    }
}
