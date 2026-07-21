namespace Kaleido.UnitTests.Queryable;

public sealed class QueryableRecordExecutorTests
{
    private readonly QueryableRecordExecutor<TestRecord> _sut;

    public QueryableRecordExecutorTests()
    {
        _sut = new QueryableRecordExecutor<TestRecord>();
    }

    [Fact]
    public async Task CountAsync_Should_Return_Record_Count()
    {
        var query = TestData.Records().AsQueryable();

        var result = await _sut.CountAsync(query);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task CountAsync_Should_Return_Zero_When_Query_Is_Empty()
    {
        var query = Array.Empty<TestRecord>()
            .AsQueryable();

        var result = await _sut.CountAsync(query);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CountAsync_Should_Throw_When_Cancellation_Is_Requested()
    {
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.CountAsync(
                TestData.Records().AsQueryable(),
                cts.Token));
    }

    [Fact]
    public async Task ToListAsync_Should_Return_All_Records()
    {
        var query = TestData.Records()
            .AsQueryable();

        var result = await _sut.ToListAsync(query);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ToListAsync_Should_Preserve_Query_Order()
    {
        var query = TestData.Records()
            .OrderByDescending(x => x.Id)
            .AsQueryable();

        var result = await _sut.ToListAsync(query);

        Assert.Equal(
            [3, 2, 1],
            result.Select(x => x.Id));
    }

    [Fact]
    public async Task ToListAsync_Should_Return_Empty_List_When_Query_Is_Empty()
    {
        var query = Array.Empty<TestRecord>()
            .AsQueryable();

        var result = await _sut.ToListAsync(query);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ToListAsync_Should_Throw_When_Cancellation_Is_Requested()
    {
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ToListAsync(
                TestData.Records().AsQueryable(),
                cts.Token));
    }

    private static class TestData
    {
        public static IReadOnlyList<TestRecord> Records()
        {
            return
            [
                new TestRecord { Id = 1 },
                new TestRecord { Id = 2 },
                new TestRecord { Id = 3 }
            ];
        }
    }

    private sealed class TestRecord
    {
        public int Id { get; init; }
    }
}