using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

// One build's registry entry as a store keeps it: the same KaleidoRegistry every participant speaks.
public sealed class KaleidoRegistryEntryExtensionsTests
    : SutFixture
{
    private static KaleidoRegistry Built(params string[] steps) => Entry([ProcessorWith("radiology", steps)], [SourceWith("radiology", "ScanSource", "ScanCard")]);

    [Fact]
    public void ToEntryJson_ThenToRegistryEntry_GivesTheSameEntry()
    {
        var back = Built("OrderScanStep").ToEntryJson().ToRegistryEntry();

        Assert.Equal(["OrderScanStep"], Assert.Single(back.Processes).Steps!.Select(x => x.Name));
        Assert.Equal(["ScanCard"], Assert.Single(back.Queryables).Views.Select(x => x.Name));
    }

    [Fact]
    public void ToRegistryEntry_OfNull_IsEmpty()
    {
        var empty = "null".ToRegistryEntry();

        Assert.Empty(empty.Processes);
        Assert.Empty(empty.Queryables);
    }

    // Instances of one build share a revision; another build has another one.
    [Fact]
    public void ToRevision_IsStableForTheSameEntry_AndDiffersForOthers()
    {
        var revision = Built("OrderScanStep").ToEntryJson().ToRevision();

        Assert.Equal(revision, Built("OrderScanStep").ToEntryJson().ToRevision());
        Assert.NotEqual(revision, Built("OrderScanStep", "ReviewStep").ToEntryJson().ToRevision());
        Assert.Matches("^[0-9a-f]{64}$", revision);
    }
}
