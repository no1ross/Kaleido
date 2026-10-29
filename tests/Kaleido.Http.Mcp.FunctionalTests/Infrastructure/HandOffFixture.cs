namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host with a second Kaleido service, "ward", behind it (its own TestServer, aggregated
/// into the clinic's registry), and the clinic step that hands a visit off to it (#503).
/// </summary>
public sealed class HandOffFixture : McpClinicFixture
{
    public HandOffFixture()
        : base(withDownstreamThatIsDown: false, withWard: true)
    {
    }
}
