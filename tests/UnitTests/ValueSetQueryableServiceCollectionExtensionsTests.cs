//using Microsoft.Extensions.DependencyInjection;
//using Xunit;
//using Kaleido.Queryable;
//using Kaleido.Attributes;
//using Kaleido;
//namespace Queryable.Tests;

//public sealed class RecordQueryableServiceCollectionExtensionsTests
//{
//    //[Fact]
//    //public void AddQueryableRecordsFromAssembly_Should_Register_Source_NamedQuery_And_Catalog()
//    //{
//    //    var services = new ServiceCollection();
//    //    services.AddQueryableRecordsFromAssembly(typeof(ScanSource).Assembly);
//    //    using var provider = services.BuildServiceProvider();
//    //    Assert.NotNull(provider.GetService<IKaleidoCatalog>());
//    //    Assert.NotNull(provider.GetService<IQueryableRecordSource<ScanRecord>>());
//    //    Assert.Single(provider.GetServices<IQueryableRecordNamedQuery<ScanRecord>>());
//    //}

//    //[KaleidoRecord("Scan", "1", "Demo")]
//    //[AllowedQuery("all", "All")]
//    //private sealed class ScanRecord
//    //{
//    //    [Filterable(FilterOperator.Eq)]
//    //    public string Name { get; init; } = string.Empty;
//    //}

//    //private sealed class ScanSource : IQueryableRecordSource<ScanRecord>
//    //{
//    //    public IQueryable<ScanRecord> CreateQuery(RecordExecutionContext context) => new[] { new ScanRecord { Name = "A" } }.AsQueryable();
//    //}

//    //private sealed class ScanQuery : IQueryableRecordNamedQuery<ScanRecord>
//    //{
//    //    public string Name => "all";
//    //    public IQueryable<ScanRecord> Apply(IQueryable<ScanRecord> query, IReadOnlyDictionary<string, object?>? parameters) => query;
//    //}
//}
