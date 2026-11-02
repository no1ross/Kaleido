using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryCapabilitiesTests
    : SutFixture
{
    private static RegistryCapabilities CreateSut(params string[] steps) => new([ProcessorWith("radiology", steps)], [SourceWith("radiology", "ScanSource", "ScanCard")]);

    [Fact]
    public void ToJson_ThenFromJson_GivesTheSameCapabilities()
    {
        var sut = CreateSut("OrderScanStep");

        var back = RegistryCapabilities.FromJson(sut.ToJson());

        Assert.Equal(["OrderScanStep"], Assert.Single(back.Processes).Steps!.Select(x => x.Name));
        Assert.Equal(["ScanCard"], Assert.Single(back.Queryables).Views.Select(x => x.Name));
    }

    [Fact]
    public void FromJson_OfNull_IsEmpty()
    {
        var empty = RegistryCapabilities.FromJson("null");

        Assert.Empty(empty.Processes);
        Assert.Empty(empty.Queryables);
    }

    // Instances of one build share a revision; another build has another one.
    [Fact]
    public void RevisionOf_IsStableForTheSameCapabilities_AndDiffersForOthers()
    {
        var revision = RegistryCapabilities.RevisionOf(CreateSut("OrderScanStep").ToJson());

        Assert.Equal(revision, RegistryCapabilities.RevisionOf(CreateSut("OrderScanStep").ToJson()));
        Assert.NotEqual(revision, RegistryCapabilities.RevisionOf(CreateSut("OrderScanStep", "ReviewStep").ToJson()));
        Assert.Matches("^[0-9a-f]{64}$", revision);
    }
}
