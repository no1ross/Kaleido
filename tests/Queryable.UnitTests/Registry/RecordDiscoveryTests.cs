//using Kaleido.Queryable.Attributes;
//using Kaleido.Queryable.Metadata;
//using Kaleido.Queryable.Registry;
//using Xunit;

//namespace Kaleido.Queryable.Tests.Registry;

//public sealed class QueryableDiscoveryTests
//{
//    [Fact]
//    public void Scan_ShouldDiscoverRecords()
//    {
//        // Arrange
//        var assemblies =
//            new[]
//            {
//                typeof(TestRecord).Assembly
//            };

//        // Act
//        var result =
//            QueryableDiscovery.Scan(assemblies);

//        // Assert
//        var record =
//            Assert.Single(
//                result.Records,
//                x => x.RecordType == typeof(TestRecord));

//        Assert.Equal(
//            typeof(TestRecord),
//            record.RecordType);

//        Assert.Equal(
//            "TestRecord",
//            record.RecordName);
//    }

//    [Fact]
//    public void Scan_ShouldDiscoverSources()
//    {
//        // Arrange
//        var assemblies =
//            new[]
//            {
//                typeof(TestRecordSource).Assembly
//            };

//        // Act
//        var result =
//            QueryableDiscovery.Scan(assemblies);

//        // Assert
//        var source =
//            Assert.Single(
//                result.Sources,
//                x => x.ImplementationType == typeof(TestRecordSource));

//        Assert.Equal(
//            typeof(TestRecord),
//            source.RecordType);

//        Assert.Equal(
//            typeof(IQueryableRecordSource<TestRecord>),
//            source.InterfaceType);

//        Assert.Equal(
//            typeof(TestRecordSource),
//            source.ImplementationType);
//    }

//    [Fact]
//    public void Scan_ShouldDiscoverNamedQueries()
//    {
//        // Arrange
//        var assemblies =
//            new[]
//            {
//            typeof(TestNamedQueryWithDependency).Assembly
//            };

//        // Act
//        var result =
//            QueryableDiscovery.Scan(assemblies);

//        // Assert
//        var query =
//            Assert.Single(
//                result.NamedQueries,
//                x => x.ImplementationType ==
//                     typeof(TestNamedQueryWithDependency));

//        Assert.Equal(
//            typeof(TestRecord),
//            query.RecordType);

//        Assert.Equal(
//            "DependencyQuery",
//            query.Name);

//        Assert.Equal(
//            "Dependency Query",
//            query.Description);

//        Assert.Equal(
//            2,
//            query.Parameters.Count);

//        Assert.Contains(
//            query.Parameters,
//            x =>
//                x.Name == nameof(TestRecord.Name) &&
//                x.Type == typeof(string));

//        Assert.Contains(
//            query.Parameters,
//            x =>
//                x.Name == nameof(TestRecord.Id) &&
//                x.Type == typeof(int));
//    }

//    [Fact]
//    public void Scan_ShouldPopulatePageableMetadata()
//    {
//        // Arrange
//        var assemblies =
//            new[]
//            {
//                typeof(TestRecord).Assembly
//            };

//        // Act
//        var result =
//            QueryableDiscovery.Scan(assemblies);

//        // Assert
//        var record =
//            Assert.Single(
//                result.Records,
//                x => x.RecordType == typeof(TestRecord));

//        Assert.NotNull(record.Pageable);

//        Assert.Equal(
//            25,
//            record.Pageable!.DefaultSize);

//        Assert.Equal(
//            100,
//            record.Pageable.MaxSize);
//    }

//    [Fact]
//    public void Scan_ShouldPopulateFieldMetadata()
//    {
//        // Arrange
//        var assemblies =
//            new[]
//            {
//                typeof(TestRecord).Assembly
//            };

//        // Act
//        var result =
//            QueryableDiscovery.Scan(assemblies);

//        // Assert
//        var record =
//            Assert.Single(
//                result.Records,
//                x => x.RecordType == typeof(TestRecord));

//        var id =
//            Assert.Single(
//                record.Fields,
//                x => x.Name == nameof(TestRecord.Id));

//        Assert.True(id.IsFilterable);
//        Assert.True(id.IsSortable);

//        var name =
//            Assert.Single(
//                record.Fields,
//                x => x.Name == nameof(TestRecord.Name));

//        Assert.True(name.IsSearchable);
//    }

//    [Fact]
//    public void Scan_ShouldIgnoreDuplicateAssemblies()
//    {
//        // Arrange
//        var assembly =
//            typeof(TestRecord).Assembly;

//        // Act
//        var result =
//            QueryableDiscovery.Scan(
//                new[]
//                {
//                    assembly,
//                    assembly
//                });

//        // Assert
//        Assert.Single(
//            result.Records,
//            x => x.RecordType == typeof(TestRecord));
//    }

//    [KaleidoRecord("TestRecord", "Test")]
//    [Pageable(25, 100)]
//    internal sealed record TestRecord(
//        [property: Filterable]
//        [property: Sortable]
//        int Id,

//        [property: Searchable]
//        string Name);

//    internal sealed class TestRecordSource :
//        IQueryableRecordSource<TestRecord>
//    {
//        public IQueryable<TestRecord> CreateQuery(
//            RecordExecutionContext executionContext)
//        {
//            return Enumerable
//                .Empty<TestRecord>()
//                .AsQueryable();
//        }
//    }

//    internal sealed class TestNamedQuery :
//        IQueryableRecordNamedQuery<TestRecord>
//    {
//        public NamedQueryMetadata Descriptor =>
//            new(
//                "Active",
//                "Active Records",
//                null);

//        public IQueryable<TestRecord> Apply(
//            IQueryable<TestRecord> query,
//            KaleidoNamedQuery namedQuery)
//        {
//            return query;
//        }
//    }

//    internal interface ITestDependency
//    {
//    }

//    [NamedQuery("DependencyQuery", "Dependency Query")]
//    [NamedQueryParameter(nameof(TestRecord.Name), typeof(string), false)]
//    [NamedQueryParameter(nameof(TestRecord.Id), typeof(int), false)]
//    internal sealed class TestNamedQueryWithDependency :
//        IQueryableRecordNamedQuery<TestRecord>
//    {
//        public TestNamedQueryWithDependency(
//            ITestDependency dependency)
//        {
//        }

//        public NamedQueryMetadata Descriptor =>
//            new(
//                "DependencyQuery",
//                "Dependency Query",
//                null);

//        public IQueryable<TestRecord> Apply(
//            IQueryable<TestRecord> query,
//            KaleidoNamedQuery namedQuery)
//        {
//            return query;
//        }
//    }
//}