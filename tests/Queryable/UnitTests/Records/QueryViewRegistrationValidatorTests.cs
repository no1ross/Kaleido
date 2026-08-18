using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Records;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryViewRegistrationValidatorTests
{
    private readonly QueryViewRegistrationValidator _validator = new();

    [Fact]
    public void Validate_WhenViewTypesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(
                null!,
                [typeof(TestContext)],
                new ServiceCollection()));
    }

    [Fact]
    public void Validate_WhenContextTypesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(
                [typeof(TestView)],
                null!,
                new ServiceCollection()));
    }

    [Fact]
    public void Validate_WhenServicesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(
                [typeof(TestView)],
                [typeof(TestContext)],
                null!));
    }

    [Fact]
    public void Validate_WhenRegistrationsAreValid_DoesNotThrow()
    {
        _validator.Validate(
            [typeof(TestView)],
            [typeof(TestContext)],
            new ServiceCollection());
    }

    [Fact]
    public void Validate_WhenDuplicateNamesExist_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(TestView), typeof(DuplicateTestView)],
                [typeof(TestContext)],
                new ServiceCollection()));

        Assert.Contains("Duplicate query view names detected", exception.Message);
    }

    [Fact]
    public void Validate_WhenViewDoesNotImplementQueryViewSource_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(InvalidView)],
                [typeof(TestContext)],
                new ServiceCollection()));

        Assert.Contains("must implement IQueryViewSource", exception.Message);
    }

    [Fact]
    public void Validate_WhenViewReferencesUnregisteredContext_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _validator.Validate(
                [typeof(TestView)],
                Array.Empty<Type>(),
                new ServiceCollection()));

        Assert.Contains("references unregistered query context", exception.Message);
    }


    [QueryContext(Name = "test-context", Version = "1.0.0")]
    private sealed class TestContext
    {
    }

    [QueryView(Name = "test-view", Version = "1.0.0")]
    private sealed class TestView : IQueryViewSource<TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(Name = "test-view", Version = "1.0.0")]
    private sealed class DuplicateTestView : IQueryViewSource<TestContext, TestContract>
    {
        public IQueryable<TestContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestContract>().AsQueryable();
    }

    [QueryView(Name = "invalid-view", Version = "1.0.0")]
    private sealed class InvalidView
    {
    }

    private sealed class TestContract
    {
    }
}
