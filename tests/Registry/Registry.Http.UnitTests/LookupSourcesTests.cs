using Kaleido.Platform.Processor;
using Kaleido.Platform.Queryable;

namespace Kaleido.Server.Registry.UnitTests;

public sealed class LookupSourcesTests : Kaleido.UnitTests.SutFixture
{
    private static readonly QueryableSourceResponse Member = new() { ServiceName = "member", Name = "MemberSource" };
    private static readonly QueryableSourceResponse Chart = new() { ServiceName = "clinic", Name = "ChartSource" };

    private static LookupSources CreateSut() => new([Member, Chart]);

    private static ProcessFieldLookup Lookup(string service, string source) =>
        new() { Service = service, Source = source, Field = "Id", Group = source };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("MEMBER", "membersource", "member")]
    [InlineData("clinic", "ChartSource", "clinic")]
    public async Task FindAsync_ResolvesTheSourceInTheCentralRegistry(string service, string name, string expected)
    {
        Assert.Equal(expected, (await CreateSut().FindAsync(Lookup(service, name), Cancel))?.ServiceName);
    }

    [Fact]
    public async Task FindAsync_AnUnknownSource_IsNotFound() =>
        Assert.Null(await CreateSut().FindAsync(Lookup("nowhere", "GhostSource"), Cancel));

    [Fact]
    public async Task FindAsync_WithoutALookup_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().FindAsync(null!, Cancel));

    [Fact]
    public async Task FindAsync_WhenCanceled_Stops()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().FindAsync(Lookup("member", "MemberSource"), canceled.Token));
    }
}
