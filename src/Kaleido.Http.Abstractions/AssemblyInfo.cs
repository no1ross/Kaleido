using System.Runtime.CompilerServices;

// Framework projects
[assembly: InternalsVisibleTo("Kaleido.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Http")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client")]

// Test projects
[assembly: InternalsVisibleTo("Kaleido.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Abstractions.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
