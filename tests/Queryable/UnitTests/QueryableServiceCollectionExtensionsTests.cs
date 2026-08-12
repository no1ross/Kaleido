using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryable_ShouldThrow_WhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => QueryableServiceCollectionExtensions.AddQueryable(
                builder: null!));
    }

    [Fact]
    public void AddQueryable_ShouldThrow_WhenConfigureIsNull()
    {
        var builder =
            CreateBuilder();

        Assert.Throws<ArgumentNullException>(
            () => QueryableServiceCollectionExtensions.AddQueryable(
                builder,
                null!));
    }

    [Fact]
    public void AddQueryable_ShouldThrow_WhenNoAssembliesRegistered()
    {
        var builder =
            new TestKaleidoBuilder(
                new ServiceCollection(),
                []);

        Assert.Throws<InvalidOperationException>(
            () => builder.AddQueryable());
    }

    [Fact]
    public void AddQueryable_ShouldRegisterQueryableCatalog()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(IQueryableService));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordRegistry()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(IQueryContextRegistry));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordRegistrationValidator()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(RecordRegistrationValidator));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordSource()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IRecordSource<TestRecord>) &&
                x.ImplementationType == typeof(TestRecordSource));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterNamedQueries()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(INamedQuery<TestRecord>) &&
                x.ImplementationType == typeof(TestNamedQuery));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordQueryEngine()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IQueryContextEngine<TestRecord>) &&
                x.ImplementationType == typeof(QueryContextEngine<TestRecord>));
    }

    [Fact]
    public void AddQueryable_ShouldResolveQueryRequestCompiler()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        using var provider =
            services.BuildServiceProvider();

        var compiler =
            provider.GetRequiredService<IQueryContextCompiler>();

        Assert.NotNull(compiler);
        Assert.IsType<Kaleido.Queryable.Query.QueryRequestCompiler>(compiler);
    }

    [Fact]
    public void AddQueryable_ShouldResolveQueryRequestValidator()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        using var provider =
            services.BuildServiceProvider();

        var validator =
            provider.GetRequiredService<IQueryContextValidator>();

        Assert.NotNull(validator);
        Assert.IsType<QueryRequestValidator>(validator);
    }

    [Fact]
    public void AddQueryable_ShouldResolveRecordRegistry()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        using var provider =
            services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IQueryContextRegistry>();

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Registrations);
    }

    private static IKaleidoBuilder CreateBuilder(
        IServiceCollection? services = null)
    {
        return new TestKaleidoBuilder(
            services ?? new ServiceCollection(),
            [
                typeof(TestRecord).Assembly
            ]);
    }

    private sealed class TestKaleidoBuilder
        : IKaleidoBuilder
    {
        public TestKaleidoBuilder(
            IServiceCollection services,
            IReadOnlyCollection<System.Reflection.Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }

        public IReadOnlyCollection<System.Reflection.Assembly> Assemblies { get; }
    }

    [QueryContext(
        Name = "test-record",
        DisplayName = "Test Record",
        Version = "1.0.0",
        Source = "Unit Test")]
    internal sealed record TestRecord(
        int Id,
        string Name);

    internal sealed class TestRecordSource
        : IRecordSource<TestRecord>
    {
        public IQueryable<TestRecord> CreateQuery(
            QueryExecutionContext executionContext)
        {
            return Enumerable.Empty<TestRecord>()
                .AsQueryable();
        }
    }

    [NamedQuery(
        Name = "active",
        Version = "1.0",
        DisplayName = "Active Records")]
    internal sealed class TestNamedQuery
        : INamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(
            IQueryable<TestRecord> query,
            NamedQuery namedQuery)
        {
            return query;
        }
    }
}