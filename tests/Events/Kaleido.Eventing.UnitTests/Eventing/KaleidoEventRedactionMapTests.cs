using Kaleido.UnitTests;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventRedactionMapTests
    : SutFixture
{
    private static readonly DataClassification Phi = new("test", "phi");
    private static readonly DataClassification Pii = new("test", "pii");

    private static KaleidoEventRedactionMap CreateSut() => new();

    [Fact]
    public void RedactorFor_IsTheRedactorOfExactlyThatSet()
    {
        var sut = CreateSut();
        sut.SetRedactor(new DataClassificationSet(Pii), typeof(NullRedactor));

        Assert.Equal(typeof(NullRedactor), sut.RedactorFor(new DataClassificationSet(Pii)));
        Assert.Null(sut.RedactorFor(new DataClassificationSet(Phi)));
        Assert.Null(sut.RedactorFor(new DataClassificationSet(Phi, Pii)));
    }

    [Fact]
    public void RedactorFor_AnUnmappedSet_IsTheFallback()
    {
        var sut = CreateSut();
        sut.Fallback = typeof(NullRedactor);

        Assert.Equal(typeof(NullRedactor), sut.RedactorFor(new DataClassificationSet(Phi)));
    }

    [Fact]
    public void SetRedactor_AgainForTheSameSet_ReplacesIt()
    {
        var sut = CreateSut();
        sut.SetRedactor(new DataClassificationSet(Pii), typeof(object));

        sut.SetRedactor(new DataClassificationSet(Pii), typeof(NullRedactor));

        Assert.Equal(typeof(NullRedactor), sut.RedactorFor(new DataClassificationSet(Pii)));
    }

    [Fact]
    public void SetRedactor_WithNullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().SetRedactor(null!, typeof(NullRedactor)));
        Assert.Throws<ArgumentNullException>(() => CreateSut().SetRedactor(new DataClassificationSet(Pii), null!));
    }
}
