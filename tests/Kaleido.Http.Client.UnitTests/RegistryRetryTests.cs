namespace Kaleido.Http.Client.UnitTests;

// #553: while there is nothing from the registry to follow, or a call failed: 1 s doubling, at most 30 s.
public sealed class RegistryRetryTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(40, 30)]
    public void TheRetry_DoublesUpTo30Seconds(int attempt, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), RegistryRetry.After(attempt));
    }
}
