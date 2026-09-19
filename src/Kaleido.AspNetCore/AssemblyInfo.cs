using System.Runtime.CompilerServices;

// Framework projects that need access to internals
[assembly: InternalsVisibleTo("Kaleido.Http")]
[assembly: InternalsVisibleTo("Kaleido.Registry")]
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore")]

// Test projects (current names)
[assembly: InternalsVisibleTo("Kaleido.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.UnitTests")]

// Test projects (future names)
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
