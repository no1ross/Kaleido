using Kaleido.Queryable.Query;
using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable.UnitTests.Runtime;

public sealed class CompiledQueryApplierTests
{
    private sealed class FakeRecord
    {
        public string Name { get; set; } = string.Empty;
    }

    private static CompiledQueryApplier<FakeRecord> CreateSut() => new();

    [Fact]
    public void ApplyFilter_WhenFilterIsNull_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplyFilter(source, filter: null);

        Assert.Equal(source, result);
    }

    [Fact]
    public void ApplySearch_WhenSearchIsNull_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplySearch(source, search: null);

        Assert.Equal(source, result);
    }

    [Fact]
    public void ApplySort_WhenSortIsEmpty_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplySort(source, sort: []);

        Assert.Equal(source, result);
    }
}
