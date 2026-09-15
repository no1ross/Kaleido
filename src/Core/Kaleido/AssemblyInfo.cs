using System.Runtime.CompilerServices;

// Test projects
[assembly: InternalsVisibleTo("Kaleido.UnitTests")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.UnitTests")]

// Framework projects that share Core internals
[assembly: InternalsVisibleTo("Kaleido.Queryable")]
[assembly: InternalsVisibleTo("Kaleido.Process")]
[assembly: InternalsVisibleTo("Kaleido.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Process.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Queryable.AspNetCore")]
[assembly: InternalsVisibleTo("Kaleido.Registry")]