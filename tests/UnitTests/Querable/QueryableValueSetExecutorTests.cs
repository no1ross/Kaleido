//using Kaleido.Queryable;
//using Queryable.Tests;
//using Xunit;
//namespace Kaleido.UnitTests.Querable;

//public sealed class QueryableRecordExecutorTests
//{
//    [Fact]
//    public async Task CountAsync_Should_Count()
//    {
//        var executor = new QueryableRecordExecutor<ClientRecord>();
//        Assert.Equal(4, await executor.CountAsync(ClientRecords.All.AsQueryable()));
//    }

//    [Fact]
//    public async Task ToListAsync_Should_Materialize()
//    {
//        var executor = new QueryableRecordExecutor<ClientRecord>();
//        var result = await executor.ToListAsync(ClientRecords.All.AsQueryable().Where(x => x.IsActive));
//        Assert.Equal(3, result.Count);
//    }
//}
