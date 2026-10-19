using System.Runtime.CompilerServices;

// Transports are built into the solution: the HTTP transport uses core's transport seams
// (authorization evaluation, correlation initialization) without making them public API.
[assembly: InternalsVisibleTo("Kaleido.Http")]

// Test projects
[assembly: InternalsVisibleTo("Kaleido.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
