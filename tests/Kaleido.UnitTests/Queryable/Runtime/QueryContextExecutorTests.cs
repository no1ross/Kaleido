using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable.Runtime.UnitTests;

public sealed class QueryContextExecutorTests
    : Kaleido.UnitTests.SutFixture
{
    private QueryContextExecutor<TestRecord> Sut =>
        CreateSut();

    private static QueryContextExecutor<TestRecord> CreateSut() =>
        new();

    [Fact]
    public async Task CountAsync_ReturnsCount()
    {
        var result = await Sut.CountAsync(TestData().AsQueryable());

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task CountAsync_WhenCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Sut.CountAsync(TestData().AsQueryable(), cts.Token));
    }

    [Fact]
    public async Task ToListAsync_ReturnsItemsInOrder()
    {
        var result = await Sut.ToListAsync(TestData().OrderByDescending(x => x.Id).AsQueryable());

        Assert.Equal([3, 2, 1], result.Select(x => x.Id));
    }

    [Fact]
    public async Task ToListAsync_WhenCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Sut.ToListAsync(TestData().AsQueryable(), cts.Token));
    }

    [Fact]
    public void ApplyPage_SkipsAndTakesRequestedRange()
    {
        var result = Sut.ApplyPage(TestData().AsQueryable(), size: 1, offset: 1).ToArray();

        var item = Assert.Single(result);
        Assert.Equal(2, item.Id);
    }

    [Fact]
    public async Task CountAsync_WhenQueryIsNull_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Sut.CountAsync(null!));

        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public async Task ToListAsync_WhenQueryIsNull_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Sut.ToListAsync(null!));

        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public async Task CountAsync_WhenQueryIsAsyncEnumerable_EnumeratesAsynchronously()
    {
        var query = new AsyncQueryable(TestData());

        var result = await Sut.CountAsync(query);

        Assert.Equal(3, result);
        Assert.True(query.AsyncEnumerated);
    }

    [Fact]
    public async Task ToListAsync_WhenQueryIsAsyncEnumerable_EnumeratesAsynchronously()
    {
        var query = new AsyncQueryable(TestData());

        var result = await Sut.ToListAsync(query);

        Assert.Equal([1, 2, 3], result.Select(x => x.Id));
        Assert.True(query.AsyncEnumerated);
    }

    [Fact]
    public async Task ToListAsync_WhenCancelledDuringAsyncEnumeration_Throws()
    {
        using var cts = new CancellationTokenSource();
        var query = new AsyncQueryable(TestData(), onItem: cts.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut.ToListAsync(query, cts.Token));
    }

    [Fact]
    public async Task CountAsync_WhenCancelledDuringAsyncEnumeration_Throws()
    {
        using var cts = new CancellationTokenSource();
        var query = new AsyncQueryable(TestData(), onItem: cts.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut.CountAsync(query, cts.Token));
    }

    private sealed class AsyncQueryable(IReadOnlyList<TestRecord> items, Action? onItem = null)
        : IQueryable<TestRecord>, IAsyncEnumerable<TestRecord>
    {
        private readonly IQueryable<TestRecord> _inner = items.AsQueryable();

        public bool AsyncEnumerated { get; private set; }

        public Type ElementType => _inner.ElementType;

        public System.Linq.Expressions.Expression Expression => _inner.Expression;

        public IQueryProvider Provider => _inner.Provider;

        public async IAsyncEnumerator<TestRecord> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            AsyncEnumerated = true;

            foreach (var item in items)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                onItem?.Invoke();
            }
        }

        public IEnumerator<TestRecord> GetEnumerator() => throw new NotSupportedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
