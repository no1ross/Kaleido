using System.ComponentModel;
using System.Reflection;
using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.Queryable.Registry.UnitTests;

public sealed class QuerySourceRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static QuerySourceRegistry CreateSut(params Type[] localSourceTypes) =>
        CreateSut(AuthorizationMetadata.Unspecified, localSourceTypes, []);

    private static QuerySourceRegistry CreateSut(
        AuthorizationMetadata defaultAuthorization,
        Type[] localSourceTypes,
        Type[] delegatedSourceTypes)
    {
        var typeDescriber = new Mock<ITypeDescriber>();
        typeDescriber
            .Setup(m => m.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(new DataTypeDescriptor("mock-type"));

        var constraintMapper = new Mock<IConstraintMapper>();
        constraintMapper
            .Setup(m => m.Map(It.IsAny<PropertyInfo>()))
            .Returns([]);

        return new QuerySourceRegistry(
            typeDescriber.Object,
            constraintMapper.Object,
            localSourceTypes,
            delegatedSourceTypes,
            defaultAuthorization);
    }

    [Fact]
    public void Constructor_BuildsLocalSourceMetadataFromSourceType()
    {
        var registry = CreateSut(typeof(TestSource));

        var registration = Assert.Single(registry.Registrations);

        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal(typeof(TestContext), registration.ResultType);
        Assert.Equal(typeof(EmptyQueryViewParameters), registration.ParametersType);
        Assert.Equal(nameof(TestSource), registration.Metadata.Name);
        Assert.Equal("Test Source", registration.Metadata.DisplayName);
        Assert.Equal("Test source description", registration.Metadata.Description);
        Assert.Equal("Unit Test", registration.Metadata.Source);
        Assert.Equal(QuerySourceKind.Local, registration.Metadata.Kind);
        Assert.Equal(25, registration.Metadata.Pageable?.DefaultSize);
        Assert.Empty(registration.Metadata.Parameters);
    }

    [Fact]
    public void Constructor_APageableLocalSource_CarriesItsDefaultSortField()
    {
        Assert.Equal(nameof(TestContext.Code), CreateSut(typeof(TestSource)).GetRegistration(typeof(TestSource)).Metadata.Pageable?.DefaultSortField);
    }

    [Theory]
    [InlineData(typeof(UnsortedPageableSource), "must define a DefaultSortField")]
    [InlineData(typeof(MissingSortFieldSource), "does not exist")]
    [InlineData(typeof(NotSortableSortFieldSource), "not marked as sortable")]
    public void Constructor_APageableLocalSource_WithoutAValidDefaultSort_FailsStartup(Type sourceType, string message)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(sourceType));

        Assert.Equal(QueryableErrorCodes.InvalidRegistration, exception.Code);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_APageableDelegatedSource_NeedsNoDefaultSort()
    {
        var registry = CreateSut(AuthorizationMetadata.Unspecified, [], [typeof(PageableDelegatedSource)]);

        var pageable = registry.GetRegistration(typeof(PageableDelegatedSource)).Metadata.Pageable;

        Assert.Equal(5, pageable?.DefaultSize);
        Assert.Null(pageable?.DefaultSortField);
    }

    [Fact]
    public void Constructor_BuildsFieldMetadataFromQueryContext()
    {
        var registry = CreateSut(typeof(TestSource));

        var registration = registry.GetRegistration(typeof(TestSource));
        var codeField = Assert.Single(registration.Metadata.Fields, x => x.Name == nameof(TestContext.Code));
        var regionField = Assert.Single(registration.Metadata.Fields, x => x.Name == nameof(TestContext.Region));

        Assert.True(codeField.IsFilterable);
        Assert.True(codeField.IsSortable);
        Assert.False(codeField.IsSearchable);

        Assert.False(regionField.IsFilterable);
        Assert.True(regionField.IsSearchable);
        Assert.Equal(1, regionField.SearchPriority);
        Assert.Equal(MatchMode.Contains, regionField.MatchMode);
        Assert.Equal("Region description", regionField.Description);
    }

    [Fact]
    public void Constructor_AllowsSeveralSourcesForOneQueryContext()
    {
        var registry = CreateSut(typeof(TestSource), typeof(ArchivedTestSource));

        Assert.Equal(2, registry.Registrations.Count);
        Assert.All(registry.Registrations, x => Assert.Equal(typeof(TestContext), x.QueryContextType));
        Assert.Null(registry.GetRegistration(typeof(ArchivedTestSource)).Metadata.Pageable);
    }

    [Fact]
    public void Constructor_BuildsDelegatedSourceMetadata()
    {
        var registry = CreateSut(AuthorizationMetadata.Unspecified, [], [typeof(TestDelegatedSource)]);

        var registration = registry.GetRegistration(typeof(TestDelegatedSource));

        Assert.Equal(QuerySourceKind.Delegated, registration.Metadata.Kind);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal(typeof(TestResult), registration.ResultType);
        Assert.Equal(typeof(TestParameters), registration.ParametersType);
        Assert.Equal(nameof(TestParameters.ProcessId), Assert.Single(registration.Metadata.Parameters).Name);
        Assert.Equal(nameof(TestResult.ProviderName), Assert.Single(registration.Metadata.OutputFields).Name);
        Assert.Equal(nameof(TestContext.Code), registration.Metadata.Fields[0].Name);
    }

    [Fact]
    public void FindAndGetRegistration_AreCaseInsensitiveByName()
    {
        var registry = CreateSut(typeof(TestSource));

        Assert.NotNull(registry.Find("TESTSOURCE"));
        Assert.Equal(typeof(TestSource), registry.GetRegistration("testsource").SourceType);
    }

    [Fact]
    public void GetRegistration_WhenNameIsMissing_Throws()
    {
        var registry = CreateSut(typeof(TestSource));

        var exception = Assert.Throws<KaleidoFrameworkException>(() => registry.GetRegistration("missing"));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Contains("missing", exception.Message);
    }

    [Fact]
    public void GetRegistration_WhenTypeIsMissing_Throws()
    {
        var registry = CreateSut(typeof(TestSource));

        var exception = Assert.Throws<KaleidoFrameworkException>(() => registry.GetRegistration(typeof(ArchivedTestSource)));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Contains(nameof(ArchivedTestSource), exception.Message);
    }

    [Fact]
    public void Constructor_ASourceWithoutQuerySourceAttribute_FailsStartup()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(typeof(UndescribedSource)));

        Assert.Equal(QueryableErrorCodes.MissingAttribute, exception.Code);
        Assert.Contains(nameof(UndescribedSource), exception.Message);
    }

    private sealed class UndescribedSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [Fact]
    public void Constructor_MapsKaleidoAuthorizationFromSource()
    {
        var registry = CreateSut(typeof(SecuredSource));

        var authorization = registry.GetRegistration(typeof(SecuredSource)).Metadata.Authorization;

        Assert.Equal("internal", authorization.Policy);
        Assert.Equal("svc", Assert.Single(authorization.Roles));
    }

    [Fact]
    public void Constructor_WithoutKaleidoAuthorization_HasUnspecifiedAuthorization()
    {
        var registry = CreateSut(typeof(TestSource));

        Assert.Same(AuthorizationMetadata.Unspecified, registry.GetRegistration(typeof(TestSource)).Metadata.Authorization);
    }

    [Fact]
    public void Constructor_SourceWithoutAttribute_InheritsServiceAuthorization()
    {
        var rule = new AuthorizationMetadata(null, ["radiology"]);
        var registry = CreateSut(rule, [typeof(TestSource)], []);

        Assert.Same(rule, registry.GetRegistration(typeof(TestSource)).Metadata.Authorization);
    }

    [Fact]
    public void Constructor_SourceAttribute_ReplacesServiceAuthorization()
    {
        var rule = new AuthorizationMetadata(null, ["radiology"]);
        var registry = CreateSut(rule, [typeof(SecuredSource)], []);

        var authorization = registry.GetRegistration(typeof(SecuredSource)).Metadata.Authorization;

        Assert.Equal("internal", authorization.Policy);
        Assert.DoesNotContain("radiology", authorization.Roles);
    }

    private sealed class TestContext : IQueryContext
    {
        [Filterable(FilterOperator.Equals)]
        [Sortable]
        public string Code { get; init; } = string.Empty;

        [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
        [Description("Region description")]
        public string Region { get; init; } = string.Empty;
    }

    [QuerySource(
        DisplayName = "Test Source",
        Description = "Test source description",
        Source = "Unit Test",
        DefaultSortField = nameof(TestContext.Code))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class TestSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Archived", Description = "Archived test records.")]
    private sealed class ArchivedTestSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Secured", Description = "Secured source.")]
    [KaleidoAuthorization(Policy = "internal", Roles = "svc")]
    private sealed class SecuredSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    private sealed record TestParameters(Guid ProcessId) : IQueryParameters;

    private sealed class TestResult
    {
        public string ProviderName { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Delegated", Description = "Delegated test source.")]
    private sealed class TestDelegatedSource : IDelegatedQuerySource<TestContext, TestResult, TestParameters>
    {
        public Task<QueryResult<TestResult>> ExecuteAsync(
            IQueryRequest<TestParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<TestResult>(0, 0, 0, []));
    }

    [QuerySource(DisplayName = "Pageable delegated", Description = "Pages in its downstream.")]
    [Pageable(DefaultSize = 5, MaxSize = 10)]
    private sealed class PageableDelegatedSource : IDelegatedQuerySource<TestContext, TestResult, TestParameters>
    {
        public Task<QueryResult<TestResult>> ExecuteAsync(
            IQueryRequest<TestParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<TestResult>(0, 0, 0, []));
    }

    [QuerySource(DisplayName = "Unsorted", Description = "Pageable without a default sort.")]
    [Pageable(DefaultSize = 5, MaxSize = 10)]
    private sealed class UnsortedPageableSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Missing", Description = "Default sort on a missing field.", DefaultSortField = "Nope")]
    [Pageable(DefaultSize = 5, MaxSize = 10)]
    private sealed class MissingSortFieldSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Not sortable", Description = "Default sort on a field that is not sortable.", DefaultSortField = nameof(TestContext.Region))]
    [Pageable(DefaultSize = 5, MaxSize = 10)]
    private sealed class NotSortableSortFieldSource : IQuerySource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }
}
