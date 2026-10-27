namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host with its guided-lookup step (<see cref="AssignBedStep"/>), whose patient is picked
/// from the Patients search (#452). Given a key-ring folder, it is one instance of a deployment whose
/// Data Protection keys live there.
/// </summary>
public sealed class LookupFixture : McpClinicFixture
{
    public LookupFixture()
        : base(withDownstreamThatIsDown: false, withLookupStep: true)
    {
    }

    // Built by the tests themselves (several instances), so not a constructor xUnit chooses from.
    internal LookupFixture(string keyRing, string applicationName = "clinic")
        : base(withDownstreamThatIsDown: false, withLookupStep: true, keyRing: keyRing, applicationName: applicationName)
    {
    }

    /// <summary>How often the clinic service ran the assign-bed step.</summary>
    public int AssignBedRuns() =>
        ServiceRequests.Count(x => x.Path.EndsWith("/assignbedstep", StringComparison.OrdinalIgnoreCase));
}
