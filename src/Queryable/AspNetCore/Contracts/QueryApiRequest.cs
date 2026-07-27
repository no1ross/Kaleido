using Kaleido.Queryable.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public record QueryApiRequest(
    QueryBody? Query = null
);

public record NamedQueryApiRequest(
    IReadOnlyDictionary<string, object?>? Values = null
);
