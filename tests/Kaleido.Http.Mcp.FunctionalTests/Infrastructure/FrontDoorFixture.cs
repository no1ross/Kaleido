namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host with <c>Kaleido:Mcp:ToolListing = FrontDoor</c>: only the starting steps, their
/// lookup queries and the built-ins are listed; open steps arrive in <c>next</c> and run with
/// <c>call_tool</c>.
/// </summary>
public sealed class FrontDoorFixture : McpClinicFixture
{
    public FrontDoorFixture()
        : base(withDownstreamThatIsDown: false, withFrontDoor: true)
    {
    }
}
