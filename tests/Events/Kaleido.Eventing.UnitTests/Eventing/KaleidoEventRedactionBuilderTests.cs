using Kaleido.UnitTests;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventRedactionBuilderTests
    : SutFixture
{
    private static readonly DataClassification Phi = new("test", "phi");
    private static readonly DataClassification Pii = new("test", "pii");

    private static KaleidoEventRedactionBuilder CreateSut(IServiceCollection services, KaleidoEventRedactionMap map) => new(services, map);

    // The event policy only: never the host's log redaction (no IRedactorProvider is registered).
    [Fact]
    public void SetRedactor_MapsEachSet_AndRegistersTheRedactorOnce_WithoutAnIRedactorProvider()
    {
        var services = new ServiceCollection();
        var map = new KaleidoEventRedactionMap();
        var sut = CreateSut(services, map);

        Assert.Same(sut, sut.SetRedactor<NullRedactor>(new DataClassificationSet(Pii), new DataClassificationSet(Phi, Pii)));
        sut.SetRedactor<NullRedactor>(new DataClassificationSet(Phi));

        Assert.Equal(typeof(NullRedactor), map.RedactorFor(new DataClassificationSet(Pii)));
        Assert.Equal(typeof(NullRedactor), map.RedactorFor(new DataClassificationSet(Phi, Pii)));
        Assert.Single(services, x => x.ServiceType == typeof(NullRedactor) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IRedactorProvider));
        Assert.Same(services, sut.Services);
    }

    [Fact]
    public void SetFallbackRedactor_IsUsedForUnmappedSets()
    {
        var services = new ServiceCollection();
        var map = new KaleidoEventRedactionMap();

        CreateSut(services, map).SetFallbackRedactor<NullRedactor>();

        Assert.Equal(typeof(NullRedactor), map.RedactorFor(new DataClassificationSet(Phi)));
        Assert.Single(services, x => x.ServiceType == typeof(NullRedactor));
    }

    [Fact]
    public void SetRedactor_WithoutClassifications_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut(new ServiceCollection(), new KaleidoEventRedactionMap()).SetRedactor<NullRedactor>(null!));
    }
}
