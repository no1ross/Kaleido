using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable.UnitTests.Runtime;

public sealed class QueryContextExecutorTests
{
    private readonly QueryContextExecutor<TestRecord> _sut = new();

    [Fact]
    public async Task CountAsync_ReturnsCount()
    {
        var result = await _sut.CountAsync(TestData().AsQueryable());

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task CountAsync_WhenCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.CountAsync(TestData().AsQueryable(), cts.Token));
    }

    [Fact]
    public async Task ToListAsync_ReturnsItemsInOrder()
    {
        var result = await _sut.ToListAsync(TestData().OrderByDescending(x => x.Id).AsQueryable());

        Assert.Equal([3, 2, 1], result.Select(x => x.Id));
    }

    [Fact]
    public async Task ToListAsync_WhenCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.ToListAsync(TestData().AsQueryable(), cts.Token));
    }

    [Fact]
    public void ApplyPage_SkipsAndTakesRequestedRange()
    {
        var result = _sut.ApplyPage(TestData().AsQueryable(), new CompiledPage(1, 1)).ToArray();

        var item = Assert.Single(result);
        Assert.Equal(2, item.Id);
    }

    private static IReadOnlyList<TestRecord> TestData() =>
    [
        new() { Id = 1 },
        new() { Id = 2 },
        new() { Id = 3 }
    ];

    private sealed class TestRecord
    {
        public int Id { get; init; }
    }
}
