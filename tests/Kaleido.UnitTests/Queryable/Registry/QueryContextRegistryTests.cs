using System.ComponentModel;
using System.Reflection;
using Kaleido.Exceptions;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryContextRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static QueryContextRegistry CreateSut(params Type[] contextTypes)
    {
        var TypeDescriber = new Mock<ITypeDescriber>();
        TypeDescriber
            .Setup(m => m.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(new DataTypeDescriptor("mock-type"));

        var constraintMapper = new Mock<IConstraintMapper>();
        constraintMapper
            .Setup(m => m.Map(It.IsAny<PropertyInfo>()))
            .Returns([]);

        return new QueryContextRegistry(
            TypeDescriber.Object,
            constraintMapper.Object,
            CreateServices(),
            contextTypes);
    }

    [Fact]
    public void Constructor_BuildsRegistrationMetadata()
    {
        var registry = CreateSut(typeof(TestContext));

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
        var registry = CreateSut(typeof(TestContext));

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
        var registry = CreateSut(typeof(TestContext));

        Assert.NotNull(registry.Find("TEST-CONTEXT"));
        Assert.Equal(typeof(TestContext), registry.GetRegistration("test-context").ContextType);
    }

    [Fact]
    public void GetRegistration_WhenNameIsMissing_Throws()
    {
        var registry = CreateSut(typeof(TestContext));

        var exception = Assert.Throws<KaleidoFrameworkException>(() => registry.GetRegistration("missing"));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Contains("missing", exception.Message);
    }

    [Fact]
    public void Constructor_MapsKaleidoAuthorization()
    {
        var registry = CreateSut(typeof(SecuredContext));

        var registration = registry.GetRegistration(typeof(SecuredContext));

        Assert.Equal(
            "internal",
            registration.Metadata.Authorization?.Policy);

        Assert.Equal(
            "svc",
            Assert.Single(registration.Metadata.Authorization!.Roles));
    }

    [Fact]
    public void Constructor_WithoutKaleidoAuthorization_HasUnspecifiedAuthorization()
    {
        var registry = CreateSut(typeof(TestContext));

        var registration = registry.GetRegistration(typeof(TestContext));

        Assert.Same(AuthorizationMetadata.Unspecified, registration.Metadata.Authorization);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryContextSource<TestContext>, TestContextSource>();
        services.AddScoped<IQueryContextSource<SecuredContext>, SecuredContextSource>();
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

    [QueryContext(
        Name = "secured-context",
        Version = "1.0.0",
        Kind = QueryContextKind.Direct)]
    [KaleidoAuthorization(Policy = "internal", Roles = "svc")]
    private sealed class SecuredContext
    {
        [Filterable(FilterOperator.Equals)]
        public string Code { get; init; } = string.Empty;
    }

    private sealed class SecuredContextSource : IQueryContextSource<SecuredContext>
    {
        public IQueryable<SecuredContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<SecuredContext>().AsQueryable();
    }
}
