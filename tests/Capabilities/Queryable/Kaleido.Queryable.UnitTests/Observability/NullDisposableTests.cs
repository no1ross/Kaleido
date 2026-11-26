using Kaleido.UnitTests;

namespace Kaleido.Queryable.Observability.UnitTests;

public sealed class NullDisposableTests
    : SutFixture
{
    private static NullDisposable CreateSut() => NullDisposable.Instance;

    [Fact]
    public void Instance_IsOneSharedObject()
    {
        Assert.Same(CreateSut(), NullDisposable.Instance);
    }

    [Fact]
    public void Dispose_DoesNothing_AndCanBeRepeated()
    {
        var sut = CreateSut();

        sut.Dispose();
        sut.Dispose();

        Assert.Same(sut, NullDisposable.Instance);
    }
}
