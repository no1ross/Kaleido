using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
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
            x => x.ServiceType == typeof(IQueryableCatalog));
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
            x => x.ServiceType == typeof(IRecordRegistry));
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
                x.ServiceType == typeof(IRecordNamedQuery<TestRecord>) &&
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
                x.ServiceType == typeof(IRecordQueryEngine<TestRecord>) &&
                x.ImplementationType == typeof(RecordQueryEngine<TestRecord>));
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
            provider.GetRequiredService<IRecordQueryCompiler>();

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
            provider.GetRequiredService<IRecordQueryValidator>();

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
            provider.GetRequiredService<IRecordRegistry>();

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Registrations);
    }

    [Fact]
    public void AddQueryable_ShouldResolveQueryableCatalog()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        using var provider =
            services.BuildServiceProvider();

        var catalog =
            provider.GetRequiredService<IQueryableCatalog>();

        Assert.NotNull(catalog);
        Assert.IsType<QueryableCatalog>(catalog);
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

    [KaleidoRecord(
        "test-record",
        "Test Record",
        null,
        "Unit Test")]
    internal sealed record TestRecord(
        int Id,
        string Name);

    internal sealed class TestRecordSource
        : IRecordSource<TestRecord>
    {
        public IQueryable<TestRecord> CreateQuery(
            RecordExecutionContext executionContext)
        {
            return Enumerable.Empty<TestRecord>()
                .AsQueryable();
        }
    }

    [NamedQuery(
        "active",
        "Active Records")]
    internal sealed class TestNamedQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(
            IQueryable<TestRecord> query,
            NamedQuery namedQuery)
        {
            return query;
        }
    }
}