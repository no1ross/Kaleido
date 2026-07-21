//using Kaleido;
//using Kaleido.Queryable;
//using Microsoft.Extensions.DependencyInjection;
//using Queryable.Tests;
//using Xunit;
//namespace Kaleido.UnitTests.Querable;

//public sealed class QueryableRecordQueryEngineTests
//{
//    [Fact]
//    public async Task ExecuteAsync_Should_Compose_Pipeline()
//    {
//        var engine = new QueryableRecordQueryEngine<ClientRecord>(new RecordMetadataCatalog(), new RecordQueryValidator(), new RecordQueryCompiler(), new Source(), new IQueryableRecordNamedQuery<ClientRecord>[] { new Active() }, new QueryableCompiledQueryApplier<ClientRecord>(), new QueryableRecordExecutor<ClientRecord>());
//        var request = new KaleidoQueryRequest("active", new QueryBody(new QuerySearch("Blue", MatchMode.StartsWith), new QueryFilter(nameof(ClientRecord.GroupName), FilterOperator.Eq, new List<object?> { "Commercial" }), new[] { new QuerySort(nameof(ClientRecord.ClientName), SortDirection.Desc) }, new QueryPage(1, 0)));
//        var result = await engine.ExecuteTypedAsync(request);
//        Assert.Single(result.Items);
//        Assert.Equal("Blue Cross", result.Items[0].ClientName);
//    }

//    private sealed class Source : IQueryableRecordSource<ClientRecord>
//    {
//        public IQueryable<ClientRecord> CreateQuery(RecordExecutionContext context) => ClientRecords.All.AsQueryable();
//    }

//    private sealed class Active : IQueryableRecordNamedQuery<ClientRecord>
//    {
//        public string Name => "active";
//        public IQueryable<ClientRecord> Apply(IQueryable<ClientRecord> query, IReadOnlyDictionary<string, object?>? parameters) => query.Where(x => x.IsActive);
//    }
//}
