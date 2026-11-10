using Kaleido.UnitTests;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventRedactorProviderTests
    : SutFixture
{
    private static readonly DataClassification Pii = new("test", "pii");
    private static readonly DataClassification Phi = new("test", "phi");

    private static KaleidoEventRedactorProvider CreateSut(KaleidoEventRedactionMap map, IServiceProvider services) => new(map, services);

    [Fact]
    public void GetRedactor_ResolvesTheMappedRedactor()
    {
        var map = new KaleidoEventRedactionMap();
        map.SetRedactor(new DataClassificationSet(Pii), typeof(NullRedactor));
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(NullRedactor))).Returns(NullRedactor.Instance);

        Assert.Same(NullRedactor.Instance, CreateSut(map, services.Object).GetRedactor(new DataClassificationSet(Pii)));
    }

    // Unmapped and no fallback: the value is erased (null redactor).
    [Fact]
    public void GetRedactor_WithoutAMappingOrFallback_IsNull()
    {
        var services = new Mock<IServiceProvider>();

        Assert.Null(CreateSut(new KaleidoEventRedactionMap(), services.Object).GetRedactor(new DataClassificationSet(Phi)));
        services.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsConfigured_FollowsThePolicy(bool configured)
    {
        Assert.Equal(configured, CreateSut(new KaleidoEventRedactionMap { IsConfigured = configured }, Mock.Of<IServiceProvider>()).IsConfigured);
    }

    [Fact]
    public void GetRedactor_WithoutClassifications_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut(new KaleidoEventRedactionMap(), Mock.Of<IServiceProvider>()).GetRedactor(null!));
    }
}
