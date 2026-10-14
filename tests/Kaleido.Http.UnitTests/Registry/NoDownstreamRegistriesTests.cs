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
    public async Task InvalidateAsync_CompletesWithoutWork()
    {
        var invalidation = CreateSut().InvalidateAsync(TestContext.Current.CancellationToken);

        Assert.True(invalidation.IsCompletedSuccessfully);
        await invalidation;
    }
}
