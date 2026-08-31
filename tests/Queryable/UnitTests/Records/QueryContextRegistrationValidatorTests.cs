using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Records;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryContextRegistrationValidatorTests
{
    private readonly QueryContextRegistrationValidator _validator = new();

    [Fact]
    public void Validate_WhenQueryContextTypesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(
                null!,
                new ServiceCollection()));
    }

    [Fact]
    public void Validate_WhenServicesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(
                [typeof(TestContext)],
                null!));
    }

    [Fact]
    public void Validate_WhenRegistrationsAreValid_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryContextSource<TestContext>, TestContextSource>();

        _validator.Validate(
            [typeof(TestContext)],
            services);
    }

    [Fact]
    public void Validate_WhenDuplicateNamesExist_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(TestContext), typeof(DuplicateContext)],
                services));

        Assert.Contains("Duplicate query context names detected", exception.Message);
    }

    [Fact]
    public void Validate_WhenSourceIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(TestContext)],
                services));

        Assert.Contains("does not have a registered source", exception.Message);
    }

    [Fact]
    public void Validate_WhenMultipleSourcesAreRegistered_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryContextSource<TestContext>, TestContextSource>();
        services.AddScoped<IQueryContextSource<TestContext>, DuplicateTestContextSource>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(TestContext)],
                services));

        Assert.Contains("has multiple registered local sources", exception.Message);
    }

    [QueryContext(Name = "test-context", Version = "1.0.0")]
    private sealed class TestContext
    {
    }

    [QueryContext(Name = "test-context", Version = "1.0.0")]
    private sealed class DuplicateContext
    {
    }

    private sealed class TestContextSource : IQueryContextSource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    private sealed class DuplicateTestContextSource : IQueryContextSource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }
}
