namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>The clinic host, but the adapter is pointed at a registry path that does not exist.</summary>
public sealed class UnreachableRegistryFixture : McpClinicFixture
{
    public UnreachableRegistryFixture()
        : base("/nowhere/registry")
    {
    }
}
