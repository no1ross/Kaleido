using System.Runtime.CompilerServices;

// Transports are built into the solution: the HTTP transport uses core's transport seams
// (authorization evaluation, correlation initialization) without making them public API.
[assembly: InternalsVisibleTo("Kaleido.Http")]

// The outgoing clients and the MCP adapter read the inbound caller the transport set (ADR 0022).
[assembly: InternalsVisibleTo("Kaleido.Http.Client")]
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp")]

// Test projects
[assembly: InternalsVisibleTo("Kaleido.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Mcp.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
