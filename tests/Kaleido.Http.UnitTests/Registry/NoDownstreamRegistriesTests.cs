using Kaleido.Http.Registry;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class NoDownstreamRegistriesTests
    : Kaleido.UnitTests.SutFixture
{
    private static NoDownstreamRegistries CreateSut() => new();

    [Fact]
    public async Task GetAsync_ReturnsNoDownstreamRegistries()
    {
        var registries = await CreateSut().GetAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(DownstreamRegistries.None, registries);
        Assert.Empty(registries.Processes);
        Assert.Empty(registries.Queryables);
        Assert.Empty(registries.Errors);
    }

    [Fact]
    public void Invalidate_DoesNothing()
    {
        var exception = Record.Exception(CreateSut().Invalidate);

        Assert.Null(exception);
    }
}
