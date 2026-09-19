using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryContextRegistryTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new QueryContextRegistry(
                null!,
                [typeof(TestContext)]));
    }

    [Fact]
    public void Constructor_WhenContextTypesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new QueryContextRegistry(
                new ServiceCollection(),
                null!));
    }

    [Fact]
    public void Constructor_BuildsRegistrationMetadata()
    {
        var registry = new QueryContextRegistry(CreateServices(), [typeof(TestContext)]);

        var registration = Assert.Single(registry.Registrations);

        Assert.Equal(typeof(TestContext), registration.ContextType);
        Assert.Equal(typeof(TestContextSource), registration.SourceType);
        Assert.Equal("test-context", registration.Metadata.Name);
        Assert.Equal("Test Context", registration.Metadata.DisplayName);
        Assert.Equal("Test context description", registration.Metadata.Description);
        Assert.Equal("1.0.0", registration.Metadata.Version);
        Assert.Equal("Unit Test", registration.Metadata.Source);
        Assert.NotNull(registration.Metadata.Pageable);
    }

    [Fact]
    public void Constructor_BuildsFieldMetadata()
    {
        var registry = new QueryContextRegistry(CreateServices(), [typeof(TestContext)]);

        var registration = registry.GetRegistration(typeof(TestContext));
        var codeField = Assert.Single(registration.Metadata.Fields, x => x.Name == nameof(TestContext.Code));
        var regionField = Assert.Single(registration.Metadata.Fields, x => x.Name == nameof(TestContext.Region));

        Assert.True(codeField.IsFilterable);
        Assert.True(codeField.IsSortable);
        Assert.False(codeField.IsSearchable);

        Assert.False(regionField.IsFilterable);
        Assert.True(regionField.IsSearchable);
        Assert.Equal(1, regionField.SearchPriority);
        Assert.Equal(MatchMode.Contains, regionField.MatchMode);
    }

    [Fact]
    public void FindAndGetRegistration_AreCaseInsensitiveByName()
    {
        var registry = new QueryContextRegistry(CreateServices(), [typeof(TestContext)]);

        Assert.NotNull(registry.Find("TEST-CONTEXT"));
        Assert.Equal(typeof(TestContext), registry.GetRegistration("test-context").ContextType);
    }

    [Fact]
    public void GetRegistration_WhenNameIsMissing_Throws()
    {
        var registry = new QueryContextRegistry(CreateServices(), [typeof(TestContext)]);

        var exception = Assert.Throws<KeyNotFoundException>(() => registry.GetRegistration("missing"));

        Assert.Contains("Query context 'missing' is not registered.", exception.Message);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryContextSource<TestContext>, TestContextSource>();
        return services;
    }

    [QueryContext(
        Name = "test-context",
        DisplayName = "Test Context",
        Description = "Test context description",
        Version = "1.0.0",
        Source = "Unit Test",
        Kind = QueryContextKind.Direct)]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class TestContext
    {
        [Filterable(FilterOperator.Equals)]
        [Sortable]
        public string Code { get; init; } = string.Empty;

        [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
        [Description("Region description")]
        public string Region { get; init; } = string.Empty;
    }

    private sealed class TestContextSource : IQueryContextSource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }
}
