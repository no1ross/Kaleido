namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic MCP host with <c>Kaleido:Mcp:RequireConfirmation</c> on (#439): its
/// <c>RequiresConfirmation</c> step (close visit) runs only after the user accepts the adapter's form.
/// </summary>
public sealed class ConfirmationFixture : McpClinicFixture
{
    public ConfirmationFixture()
        : base(withDownstreamThatIsDown: false, withConfirmation: true)
    {
    }
}
