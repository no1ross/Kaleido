using Kaleido.UnitTests;

namespace Kaleido.Process.Storage.UnitTests;

public sealed class NoProcessTransactionsTests
    : SutFixture
{
    private static NoProcessTransactions CreateSut() => new();

    // No persistence provides transactions: each store write stands alone.
    [Fact]
    public async Task BeginAsync_ReturnsATransactionWhoseCommitAndDisposeDoNothing()
    {
        await using var transaction = await CreateSut().BeginAsync(TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(transaction);
    }

    [Fact]
    public async Task BeginAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().BeginAsync(cancellation.Token));
    }
}
