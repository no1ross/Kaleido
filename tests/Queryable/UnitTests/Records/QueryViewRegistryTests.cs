using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryViewRegistryTests
{
    [Fact]
    public void Constructor_BuildsRegistrationMetadata()
    {
        var registry = new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(TestView)]);

        var registration = Assert.Single(registry.Registrations);

        Assert.Equal(typeof(TestView), registration.QueryViewType);
        Assert.Equal(typeof(TestContract), registration.ViewType);
        Assert.Equal(typeof(TestParameters), registration.ViewParametersType);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal("test-view", registration.Metadata.Name);
        Assert.Equal("Test View", registration.Metadata.DisplayName);
        Assert.Equal("Test view description", registration.Metadata.Description);
        Assert.NotNull(registration.Metadata.Pageable);
    }

    [Fact]
    public void Constructor_BuildsParameterMetadata()
    {
        var registry = new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(TestView)]);

        var parameter = Assert.Single(registry.GetRegistration(typeof(TestView)).Metadata.Parameters!);

        Assert.Equal(nameof(TestParameters.Category), parameter.Name);
        Assert.Equal(typeof(string), parameter.Type);
        Assert.Equal(DataTypeMapper.GetDescriptor(typeof(TestParameters).GetProperty(nameof(TestParameters.Category))!), parameter.DataType);
        Assert.Equal("Category description", parameter.Description);
        Assert.Single(parameter.Constraints);
        Assert.Equal("Required", parameter.Constraints.Single().Type);

        var outputField = Assert.Single(registry.GetRegistration(typeof(TestView)).Metadata.OutputFields!, x => x.Name == nameof(TestContract.Id));
        Assert.Equal(typeof(int), outputField.Type);
        Assert.Equal(DataTypeMapper.GetDescriptor(typeof(TestContract).GetProperty(nameof(TestContract.Id))!), outputField.DataType);
    }

    [Fact]
    public void Constructor_UsesEmptyParametersForTwoGenericArgumentView()
    {
        var registry = new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(SimpleView)]);

        var registration = registry.GetRegistration(typeof(SimpleView));

        Assert.Equal(typeof(EmptyQueryViewParameters), registration.ViewParametersType);
        Assert.Empty(registration.Metadata.Parameters!);
        Assert.Single(registration.Metadata.OutputFields!, x => x.Name == nameof(TestContract.Id));
    }

    [Fact]
    public void Constructor_WhenPageableViewMissingDefaultSortField_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(MissingSortView)]));

        Assert.Contains("must define a DefaultSortField", exception.Message);
    }

    [Fact]
    public void Constructor_WhenDefaultSortFieldIsNotSortable_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(NotSortableView)]));

        Assert.Contains("not marked as sortable", exception.Message);
    }

    [Fact]
    public void FindAndGetRegistration_AreCaseInsensitiveByName()
    {
        var registry = new QueryViewRegistry(new Microsoft.Extensions.DependencyInjection.ServiceCollection(), [typeof(TestView)]);

        Assert.NotNull(registry.Find("TEST-VIEW"));
        Assert.Equal(typeof(TestView), registry.GetRegistration("test-view").QueryViewType);
    }

    [QueryContext(Name = "test-context", Version = "1.0.0")]
    private sealed class TestContext
    {
        [Sortable]
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    [QueryView(
        Name = "test-view",
        DisplayName = "Test View",
        Description = "Test view description",
        Version = "1.0.0",
        DefaultSortField = nameof(TestContext.Id))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class TestView : IQueryViewSource<TestContext, TestContract, TestParameters>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(Name = "simple-view", Version = "1.0.0")]
    private sealed class SimpleView : IQueryViewSource<TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(Name = "missing-sort-view", Version = "1.0.0")]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class MissingSortView : IQueryViewSource<TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(Name = "not-sortable-view", Version = "1.0.0", DefaultSortField = nameof(TestContext.Name))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class NotSortableView : IQueryViewSource<TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    private sealed class TestContract
    {
        public int Id { get; init; }
    }

    private sealed class TestParameters
    {
        [Required]
        [Description("Category description")]
        public string Category { get; init; } = string.Empty;
    }
}
