namespace Kaleido.Http.UnitTests.Startup;

public sealed class KaleidoStartupFilterTests
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
