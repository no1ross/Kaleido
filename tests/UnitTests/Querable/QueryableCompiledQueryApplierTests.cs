using Kaleido;
using Kaleido.Metadata;
using Kaleido.Queryable;
using Queryable.Tests;
using Xunit;
namespace Kaleido.UnitTests.Querable;

public sealed class QueryableCompiledQueryApplierTests
{
    private readonly QueryableCompiledQueryApplier<ClientRecord> _applier = new();
    private readonly RuntimeRecordMetadata _metadata = RecordMetadataBuilder.Build<ClientRecord>();

    [Fact]
    public void ApplyFilter_Should_Apply_Eq()
    {
        var field = _metadata.Fields.Single(x => x.Name == nameof(ClientRecord.GroupName));
        var result = _applier.ApplyFilter(ClientRecords.All.AsQueryable(), new CompiledFilterCondition(field, FilterOperator.Eq, new object?[] { "Commercial" })).ToArray();
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void ApplyFilter_Should_Apply_Contains()
    {
        var field = _metadata.Fields.Single(x => x.Name == nameof(ClientRecord.ClientName));
        var result = _applier.ApplyFilter(ClientRecords.All.AsQueryable(), new CompiledFilterCondition(field, FilterOperator.Contains, new object?[] { "Blue" })).ToArray();
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void ApplyFilter_Should_Apply_StartsWith()
    {
        var field = _metadata.Fields.Single(x => x.Name == nameof(ClientRecord.ClientName));
        var result = _applier.ApplyFilter(ClientRecords.All.AsQueryable(), new CompiledFilterCondition(field, FilterOperator.StartsWith, new object?[] { "Blue" })).ToArray();
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void ApplySearch_Should_Apply_StartsWith()
    {
        var field = _metadata.Fields.Single(x => x.Name == nameof(ClientRecord.ClientName));
        var result = _applier.ApplySearch(ClientRecords.All.AsQueryable(), new CompiledSearchCondition(field, "Blue", MatchMode.StartsWith)).ToArray();
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void ApplySort_Should_Sort_Descending()
    {
        var field = _metadata.Fields.Single(x => x.Name == nameof(ClientRecord.ClientName));
        var result = _applier.ApplySort(ClientRecords.All.AsQueryable(), new[] { new CompiledSort(field, SortDirection.Desc, 0) }).ToArray();
        Assert.Equal("Blue Shield", result[0].ClientName);
    }

    [Fact]
    public void ApplyPage_Should_Skip_And_Take()
    {
        var result = _applier.ApplyPage(ClientRecords.All.AsQueryable(), new CompiledPage(2, 1)).ToArray();
        Assert.Equal(2, result.Length);
        Assert.Equal("Blue Shield", result[0].ClientName);
    }
}
