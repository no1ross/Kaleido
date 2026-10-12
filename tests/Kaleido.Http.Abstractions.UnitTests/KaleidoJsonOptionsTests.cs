using System.Text.Json;
using Kaleido.UnitTests;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoJsonOptionsTests
    : SutFixture
{
    // A shared options instance that is still mutable gets written to on first use (Results.Json sets
    // its type-info resolver), which races with a concurrent serialization that locks it (#270).
    [Fact]
    public void Options_AreReadOnlyFromTheStart()
    {
        Assert.True(KaleidoJsonOptions.Options.IsReadOnly);
        Assert.NotNull(KaleidoJsonOptions.Options.TypeInfoResolver);
    }

    [Fact]
    public void Options_WriteEnumsAsCamelCaseStringsAndPropertiesInCamelCase()
    {
        var json = JsonSerializer.Serialize(new Sample(SampleKind.FirstKind), KaleidoJsonOptions.Options);

        Assert.Equal("""{"sampleKind":"firstKind"}""", json);
    }

    [Fact]
    public void Options_RejectIntegerEnumValues()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Sample>("""{"sampleKind":0}""", KaleidoJsonOptions.Options));
    }

    [Fact]
    public void Options_CanBeCopiedToChangeASetting()
    {
        var copy = new JsonSerializerOptions(KaleidoJsonOptions.Options) { WriteIndented = true };

        Assert.False(copy.IsReadOnly);
        Assert.True(copy.WriteIndented);
    }

    private enum SampleKind
    {
        FirstKind,
    }

    private sealed record Sample(SampleKind SampleKind);
}
