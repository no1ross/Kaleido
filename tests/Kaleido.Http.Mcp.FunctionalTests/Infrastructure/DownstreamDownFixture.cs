namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// The clinic host aggregating a "radiology" downstream that cannot be reached: the registry is
/// partial, and the adapter keeps serving the services that answer.
/// </summary>
public sealed class DownstreamDownFixture : McpClinicFixture
{
    public DownstreamDownFixture()
        : base(withDownstreamThatIsDown: true)
    {
    }
}