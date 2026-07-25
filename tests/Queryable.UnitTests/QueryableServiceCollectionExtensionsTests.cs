using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Queryable.Tests;

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
            x =>
                x.ServiceType ==
                typeof(IQueryableCatalog) &&
                x.ImplementationType ==
                typeof(QueryableCatalog));
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
            x =>
                x.ServiceType ==
                typeof(RecordRegistrationValidator) &&
                x.ImplementationType ==
                typeof(RecordRegistrationValidator));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterAtLeastOneRecordSource()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType.IsGenericType &&
                x.ServiceType.GetGenericTypeDefinition() ==
                typeof(IQueryableRecordSource<>));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterAtLeastOneNamedQuery()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType.IsGenericType &&
                x.ServiceType.GetGenericTypeDefinition() ==
                typeof(IQueryableRecordNamedQuery<>));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterAtLeastOneRecordQueryEngine()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType.IsGenericType &&
                x.ServiceType.GetGenericTypeDefinition() ==
                typeof(IRecordQueryEngine<>));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordQueryValidator()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType ==
                typeof(IRecordQueryValidator) &&
                x.ImplementationType ==
                typeof(RecordQueryValidator));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordQueryCompiler()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType ==
                typeof(IRecordQueryCompiler) &&
                x.ImplementationType ==
                typeof(RecordQueryCompiler));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterCompiledQueryAppliers()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType.IsGenericType &&
                x.ServiceType.GetGenericTypeDefinition() ==
                typeof(IQueryableCompiledQueryApplier<>));
    }

    [Fact]
    public void AddQueryable_ShouldRegisterRecordExecutors()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        Assert.Contains(
            services,
            x =>
                x.ServiceType.IsGenericType &&
                x.ServiceType.GetGenericTypeDefinition() ==
                typeof(IQueryableRecordExecutor<>));
    }

    [Fact]
    public void AddQueryable_ShouldBuildRecordRegistrations()
    {
        var services =
            new ServiceCollection();

        CreateBuilder(services)
            .AddQueryable();

        using var provider =
            services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IRecordRegistry>();

        Assert.NotEmpty(
            registry.Registrations);
    }
    private static IKaleidoBuilder CreateBuilder(
        IServiceCollection? services = null)
    {
        return new TestKaleidoBuilder(
            services ?? new ServiceCollection(),
            [
                typeof(Kaleido.Samples.Shared.SampleKaleidoRecord).Assembly
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
}