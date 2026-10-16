using System.Runtime.CompilerServices;

// Front doors are built into the solution: the MCP adapter calls services as the caller and
// for a process through these clients' forwarding scopes, which are not public API.
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp")]

// Test projects
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
