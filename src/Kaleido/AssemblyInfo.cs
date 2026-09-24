using System.Runtime.CompilerServices;

// Test projects
[assembly: InternalsVisibleTo("Kaleido.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

// Framework projects that need access to internals
[assembly: InternalsVisibleTo("Kaleido.Http")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client")]
[assembly: InternalsVisibleTo("Kaleido.Provider.SQLite")]
