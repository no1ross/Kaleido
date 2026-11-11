namespace Kaleido.UnitTests;

public sealed class AssemblyTypeExtensionsTests
    : SutFixture
{
    public sealed class PublicNestedType;

    internal sealed class InternalNestedType;

    private sealed class PrivateNestedType;

    public abstract class AbstractNestedType;

    [Fact]
    public void ScanTypes_ReturnsConcreteVisibleClassesOnce()
    {
        var assembly = typeof(AssemblyTypeExtensionsTests).Assembly;

        var result = new[] { assembly, assembly }.ScanTypes();

        Assert.Contains(typeof(AssemblyTypeExtensionsTests), result);
        Assert.Contains(typeof(PublicNestedType), result);
        Assert.Contains(typeof(InternalNestedType), result);
        Assert.DoesNotContain(typeof(PrivateNestedType), result);
        Assert.DoesNotContain(typeof(AbstractNestedType), result);
        Assert.Single(result, x => x == typeof(PublicNestedType));
    }
}
