using Kaleido.Client;

namespace Kaleido.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The MCP host's token exchange stand-in (#574): a call made for the agent's user carries a new token for
/// that user (<see cref="BearerTestAuthHandler.Exchanged"/>), never the agent's token; the host's own calls
/// are left as the host configured them.
/// </summary>
internal sealed class TestTokenExchangeHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (KaleidoCallContext.Of(request) is { Purpose: KaleidoCallPurpose.OnBehalfOfCaller, CallerAuthorization: { } inbound })
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", BearerTestAuthHandler.Exchanged(inbound));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
