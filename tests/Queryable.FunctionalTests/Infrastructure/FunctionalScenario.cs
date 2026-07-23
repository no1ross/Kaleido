using Kaleido.Queryable;
using Kaleido.Shared;

namespace Kaleido.FunctionalTests.Infrastructure;

public sealed class FunctionalScenario
{
    public FunctionalScenario(
        string name,
        Func<IReadOnlyList<SampleKaleidoRecord>, KaleidoQueryRequest> createRequest,
        Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> expectedUnpaged,
        Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> expectedPaged)
    {
        Name = name;
        CreateRequest = createRequest;
        ExpectedUnpaged = expectedUnpaged;
        ExpectedPaged = expectedPaged;
    }

    public string Name { get; }
    public Func<IReadOnlyList<SampleKaleidoRecord>, KaleidoQueryRequest> CreateRequest { get; }
    public Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> ExpectedUnpaged { get; }
    public Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> ExpectedPaged { get; }

    public override string ToString() => Name;
}
