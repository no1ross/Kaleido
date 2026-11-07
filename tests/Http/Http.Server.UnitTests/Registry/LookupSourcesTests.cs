using Kaleido.Platform.Queryable;
using Kaleido.Platform.Registry;

namespace Kaleido.Http.Server.Registry.UnitTests;

// #524: a step's lookup is found in the full registry: the one being filtered when it is already full,
// else the registry source's, read once and only when needed.
public sealed class LookupSourcesTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly QueryableSourceResponse Member = new() { ServiceName = "member", Name = "MemberSource" };
    private static readonly QueryableSourceResponse Chart = new() { ServiceName = "clinic", Name = "ChartSource" };

    private readonly Mock<IKaleidoRegistrySource> source = new();

    public LookupSourcesTests() =>
        source.Setup(x => x.GetAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(Contents([], [Chart], []));

    private LookupSources CreateSut(bool complete) => new([Member], complete, source.Object, maxParallelism: 4);

    private static ProcessFieldLookup Lookup(string service, string source) =>
        new() { Service = service, Source = source, Field = "Id", Group = source };

    private static KaleidoRegistry Contents(IReadOnlyCollection<ProcessorRegistryResponse> processes, IReadOnlyCollection<QueryableSourceResponse> queryables, IReadOnlyCollection<RegistryReadError> errors) =>
        new() { Processes = processes, Queryables = queryables, Errors = errors };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALookupOnThisRegistry_IsFoundWithoutReadingTheSource()
    {
        var found = await CreateSut(complete: false).FindAsync(Lookup("MEMBER", "membersource"), Cancel);

        Assert.Same(Member, found);
        source.VerifyNoOtherCalls();
    }

    // A non-aggregating host reads its source for another service's query, once for all its lookups.
    [Fact]
    public async Task ALookupOnAnotherService_IsFoundInTheSource_ReadOnce()
    {
        var sut = CreateSut(complete: false);

        var chart = await sut.FindAsync(Lookup("clinic", "ChartSource"), Cancel);
        var ghost = await sut.FindAsync(Lookup("nowhere", "GhostSource"), Cancel);

        Assert.Same(Chart, chart);
        Assert.Null(ghost);
        source.Verify(x => x.GetAsync(4, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The registry being filtered is already the full one (an aggregating host): nothing else is read.
    [Fact]
    public async Task WhenTheRegistryIsFull_ALookupNotInIt_IsNotFound()
    {
        Assert.Null(await CreateSut(complete: true).FindAsync(Lookup("clinic", "ChartSource"), Cancel));
        source.VerifyNoOtherCalls();
    }
}
