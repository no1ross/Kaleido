using System.Runtime.CompilerServices;

// Framework projects (current names — will be updated as projects are renamed in later increments)
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Process.Http.Client")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.Http.Client")]
[assembly: InternalsVisibleTo("Kaleido.Registry")]

// Framework projects (future names — will take effect when projects are renamed)
[assembly: InternalsVisibleTo("Kaleido.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Http")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client")]

// Test projects (current names)
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Process.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore.FunctionalTests")]

// Test projects (future names)
[assembly: InternalsVisibleTo("Kaleido.AspNetCore.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.FunctionalTests")]
[assembly: InternalsVisibleTo("Kaleido.Http.Client.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
