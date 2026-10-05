namespace Kaleido.Http.Startup.UnitTests;

public sealed class KaleidoStartupFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoStartupFilter CreateSut() => new();

    [Fact]
    public void Configure_ReturnsNonNullAction()
    {
        var sut = CreateSut();
        var result = sut.Configure(_ => { });
        Assert.NotNull(result);
    }
}
