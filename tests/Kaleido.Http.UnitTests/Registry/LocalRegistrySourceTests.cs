namespace Kaleido.Http.Registry.UnitTests;

public sealed class LocalRegistrySourceTests
    : Kaleido.UnitTests.SutFixture
{
    private static LocalRegistrySource CreateSut() => new();

    [Fact]
    public async Task GetAsync_HasNothingBeyondTheHostsOwnRegistrations()
    {
        var registry = await CreateSut().GetAsync(maxParallelism: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(registry.Processes);
        Assert.Empty(registry.Queryables);
        Assert.Empty(registry.Errors);
        Assert.Empty(registry.Missing);
        Assert.Empty((await CreateSut().GetAvailabilityAsync(TestContext.Current.CancellationToken)).Services);
        Assert.False(CreateSut().IsFullRegistry);
    }

    [Fact]
    public async Task RefreshAsync_CompletesWithoutWork()
    {
        var refresh = CreateSut().RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(refresh.IsCompletedSuccessfully);
        await refresh;
    }
}
