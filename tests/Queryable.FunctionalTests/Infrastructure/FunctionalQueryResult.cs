
using Kaleido.Samples.Shared;

namespace Kaleido.FunctionalTests.Infrastructure;

public sealed record FunctionalQueryResult(
    IReadOnlyList<SampleKaleidoRecord> Items,
    int TotalCount);
