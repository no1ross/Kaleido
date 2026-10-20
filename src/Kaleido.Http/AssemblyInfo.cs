using System.Runtime.CompilerServices;

// The MCP adapter is a front door built into the solution: it sets up the request's correlation
// for its own endpoint (not a Kaleido route group) with the transport's internal helper.
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp")]

// Test projects
[assembly: InternalsVisibleTo("Kaleido.Http.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
