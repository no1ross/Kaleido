using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Kaleido.Exceptions;

namespace Kaleido.Queryable.Registry.UnitTests;

public sealed class QueryViewRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DataTypeDescriptor TestDataType =
        new("mock-type");

    private static QueryViewRegistry CreateSut(params Type[] viewTypes)
    {
        var typeDescriber = new Mock<ITypeDescriber>();
        typeDescriber
            .Setup(m => m.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(TestDataType);

        var constraintMapper = new Mock<IConstraintMapper>();
        constraintMapper
            .Setup(m => m.Map(It.IsAny<PropertyInfo>()))
            .Returns([new ConstraintContract { Type = "Required" }]);

        return new QueryViewRegistry(
            typeDescriber.Object,
            constraintMapper.Object,
            viewTypes);
    }

    [Fact]
    public void Constructor_BuildsRegistrationMetadata()
    {
        var registry = CreateSut(typeof(TestView));

        var registration = Assert.Single(registry.Registrations);

        Assert.Equal(typeof(TestView), registration.QueryViewType);
        Assert.Equal(typeof(TestContract), registration.ViewType);
        Assert.Equal(typeof(TestParameters), registration.ViewParametersType);
        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal(nameof(TestView), registration.Metadata.Name);
        Assert.Equal("Test View", registration.Metadata.DisplayName);
        Assert.Equal("Test view description", registration.Metadata.Description);
        Assert.NotNull(registration.Metadata.Pageable);
    }

    [Fact]
    public void Constructor_BuildsParameterMetadata()
    {
        var registry = CreateSut(typeof(TestView));

        var parameter = Assert.Single(registry.GetRegistration(typeof(TestView)).Metadata.Parameters!);

        Assert.Equal(nameof(TestParameters.Category), parameter.Name);
        Assert.Equal(typeof(string), parameter.Type);
        Assert.Equal(TestDataType, parameter.DataType);
        Assert.Equal("Category description", parameter.Description);
        Assert.Single(parameter.Constraints);
        Assert.Equal("Required", parameter.Constraints.Single().Type);

        var outputField = Assert.Single(registry.GetRegistration(typeof(TestView)).Metadata.OutputFields!, x => x.Name == nameof(TestContract.Id));
        Assert.Equal(typeof(int), outputField.Type);
        Assert.Equal(TestDataType, outputField.DataType);
    }

    [Fact]
    public void Constructor_UsesEmptyParametersForViewWithoutParameters()
    {
        var registry = CreateSut(typeof(SimpleView));

        var registration = registry.GetRegistration(typeof(SimpleView));

        Assert.Equal(typeof(EmptyQueryViewParameters), registration.ViewParametersType);
        Assert.Empty(registration.Metadata.Parameters!);
        Assert.Single(registration.Metadata.OutputFields!, x => x.Name == nameof(TestContract.Id));
    }

    [Fact]
    public void Constructor_RegistersAsyncView()
    {
        var registry = CreateSut(typeof(AsyncView));

        var registration = registry.GetRegistration(typeof(AsyncView));

        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(typeof(TestContract), registration.ViewType);
    }

    [Fact]
    public void Constructor_WhenPageableViewMissingDefaultSortField_Throws()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            CreateSut(typeof(MissingSortView)));

        Assert.Contains("must define a DefaultSortField", exception.Message);
    }

    [Fact]
    public void Constructor_WhenDefaultSortFieldIsNotSortable_Throws()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            CreateSut(typeof(NotSortableView)));

        Assert.Contains("not marked as sortable", exception.Message);
    }

    [Fact]
    public void Constructor_MapsKaleidoAuthorization()
    {
        var registry = CreateSut(typeof(SecuredView));

        var authorization = registry.GetRegistration(typeof(SecuredView)).Metadata.Authorization;

        Assert.Equal("clinician-policy", authorization?.Policy);
        Assert.Equal("clinician", Assert.Single(authorization!.Roles));
    }

    [Fact]
    public void Constructor_WithoutKaleidoAuthorization_HasNullAuthorization()
    {
        var registry = CreateSut(typeof(TestView));

        Assert.Null(registry.GetRegistration(typeof(TestView)).Metadata.Authorization);
    }

    [Fact]
    public void GetRegistration_WhenTypeIsMissing_Throws()
    {
        var registry = CreateSut(typeof(TestView));

        var exception = Assert.Throws<KaleidoFrameworkException>(() => registry.GetRegistration(typeof(SimpleView)));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
    }

    private sealed class TestContext : IQueryContext
    {
        [Sortable]
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class TestSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QueryView(
        DisplayName = "Test View",
        Description = "Test view description",
        Version = "1.0.0",
        DefaultSortField = nameof(TestContext.Id))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class TestView : IQueryViewSource<TestSource, TestContext, TestContract, TestParameters>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(DisplayName = "Simple", Description = "Simple view.", Version = "1.0.0")]
    private sealed class SimpleView : IQueryViewSource<TestSource, TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(DisplayName = "Async", Description = "Async view.", Version = "1.0.0")]
    private sealed class AsyncView : IQueryViewSourceAsync<TestSource, TestContext, TestContract>
    {
        public Task<IQueryable<TestContract>> CreateViewAsync(
            IQueryable<TestContext> query,
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<TestContract>().AsQueryable());
    }

    [QueryView(DisplayName = "Missing sort", Description = "Missing sort view.", Version = "1.0.0")]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class MissingSortView : IQueryViewSource<TestSource, TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(DisplayName = "Not sortable", Description = "Not sortable view.", Version = "1.0.0", DefaultSortField = nameof(TestContext.Name))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class NotSortableView : IQueryViewSource<TestSource, TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(DisplayName = "Secured", Description = "Secured view.", Version = "1.0.0")]
    [KaleidoAuthorization(Policy = "clinician-policy", Roles = "clinician")]
    private sealed class SecuredView : IQueryViewSource<TestSource, TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    private sealed class TestContract
    {
        public int Id { get; init; }
    }

    private sealed class TestParameters : IQueryParameters
    {
        [Required]
        [Description("Category description")]
        public string Category { get; init; } = string.Empty;
    }
}
