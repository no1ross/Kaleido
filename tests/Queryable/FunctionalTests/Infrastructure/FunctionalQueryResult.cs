
using Kaleido.Queryable.Shared;

namespace Kaleido.Queryable.FunctionalTests.Infrastructure;

public sealed record FunctionalQueryResult(
    IReadOnlyList<SampleKaleidoRecord> Items,
    int TotalCount);
