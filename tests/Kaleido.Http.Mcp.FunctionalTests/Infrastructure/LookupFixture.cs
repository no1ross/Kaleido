namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host with its guided-lookup step (<see cref="AssignBedStep"/>), whose patient is picked
/// from the Patients search (#452).
/// </summary>
public sealed class LookupFixture : McpClinicFixture
{
    public LookupFixture()
        : base(withDownstreamThatIsDown: false, withLookupStep: true)
    {
    }
}
