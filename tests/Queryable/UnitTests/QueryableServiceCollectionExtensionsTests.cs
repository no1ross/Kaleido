using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryable_WhenBuilderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QueryableServiceCollectionExtensions.AddQueryable(builder: null!));
    }

    [Fact]
    public void AddQueryable_WhenConfigureIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QueryableServiceCollectionExtensions.AddQueryable(CreateBuilder(), null!));
    }

    [Fact]
    public void AddQueryable_WhenNoAssembliesAreRegistered_Throws()
    {
        var builder = new TestKaleidoBuilder(new ServiceCollection(), []);

        Assert.Throws<InvalidOperationException>(() => builder.AddQueryable());
    }

    [Fact]
    public void AddQueryable_RegistersFrameworkServices()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddQueryable();

        Assert.Contains(services, x => x.ServiceType == typeof(IQueryableService));
        Assert.Contains(services, x => x.ServiceType == typeof(IQueryContextValidator));
        Assert.Contains(services, x => x.ServiceType == typeof(IQueryContextCompiler));
        Assert.Contains(services, x => x.ServiceType == typeof(ICompiledQueryApplier<>));
        Assert.Contains(services, x => x.ServiceType == typeof(IQueryContextExecutor<>));
        Assert.Contains(services, x => x.ServiceType == typeof(IQueryContextRegistry));
        Assert.Contains(services, x => x.ServiceType == typeof(IQueryViewRegistry));
        Assert.Contains(services, x => x.ServiceType == typeof(IDelegatedQueryViewRegistry));
        Assert.Contains(services, x => x.ServiceType == typeof(QueryContextRegistrationValidator));
        Assert.Contains(services, x => x.ServiceType == typeof(QueryViewRegistrationValidator));
    }

    [Fact]
    public void AddQueryable_RegistersOpenGenericExecutionServices()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddQueryable();

        Assert.Contains(services, x =>
            x.ServiceType == typeof(ICompiledQueryApplier<>) &&
            x.ImplementationType == typeof(CompiledQueryApplier<>));

        Assert.Contains(services, x =>
            x.ServiceType == typeof(IQueryContextExecutor<>) &&
            x.ImplementationType == typeof(QueryContextExecutor<>));
    }

    [Fact]
    public void AddQueryable_ResolvesRegistries()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddQueryable();

        using var provider = services.BuildServiceProvider();

        var contextRegistry = provider.GetRequiredService<IQueryContextRegistry>();
        var viewRegistry = provider.GetRequiredService<IQueryViewRegistry>();
        var delegatedViewRegistry = provider.GetRequiredService<IDelegatedQueryViewRegistry>();

        Assert.NotNull(contextRegistry);
        Assert.NotNull(viewRegistry);
        Assert.NotNull(delegatedViewRegistry);
    }

    [Fact]
    public void AddQueryable_IsIdempotentForSingletonFrameworkServices()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services);

        builder.AddQueryable();
        builder.AddQueryable();

        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IQueryableService)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IQueryContextValidator)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IQueryContextCompiler)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IQueryContextRegistry)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IQueryViewRegistry)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IDelegatedQueryViewRegistry)));
    }

    [Fact]
    public void AddQueryable_RegistersDelegatedViewInDelegatedRegistry()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddQueryable();

        using var provider = services.BuildServiceProvider();

        var registration = provider
            .GetRequiredService<IDelegatedQueryViewRegistry>()
            .GetRegistration(typeof(DelegatedView));

        Assert.Equal(typeof(DelegatedViewContract), registration.ViewType);
        Assert.Equal(typeof(DelegatedContext), registration.QueryContextType);
        Assert.Equal(QueryContextKind.Delegated, registration.QueryMetadata.Kind);

        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IQueryContextEngine<,>).MakeGenericType(typeof(DelegatedContext), typeof(DelegatedContext)));
    }

    private static IKaleidoBuilder CreateBuilder(IServiceCollection? services = null) =>
        new TestKaleidoBuilder(
            services ?? new ServiceCollection(),
            [typeof(TestContext).Assembly]);

    private sealed class TestKaleidoBuilder : IKaleidoBuilder
    {
        public TestKaleidoBuilder(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }

    [QueryContext(
        Name = "test-context",
        DisplayName = "Test Context",
        Description = "Test Context",
        Version = "1.0.0",
        Source = "Unit Test",
        Kind = QueryContextKind.Direct)]
    private sealed class TestContext
    {
        [Sortable]
        public int Id { get; init; }
    }

    private sealed class TestContextSource : IQueryContextSource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    [QueryView(
        Name = "test-view",
        DisplayName = "Test View",
        Description = "Test View",
        Version = "1.0.0",
        DefaultSortField = nameof(TestContext.Id))]
    [Pageable(DefaultSize = 25, MaxSize = 100)]
    private sealed class TestView : IQueryViewSource<TestContext, TestViewContract, TestViewParameters>
    {
        public IQueryable<TestViewContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<TestViewContract>().AsQueryable();
    }

    private sealed class TestViewContract
    {
    }

    private sealed class TestViewParameters
    {
        public string Category { get; init; } = string.Empty;
    }

    [QueryContext(
        Name = "delegated-context",
        DisplayName = "Delegated Context",
        Description = "Delegated Context",
        Version = "1.0.0",
        Source = "Unit Test",
        Kind = QueryContextKind.Delegated)]
    public sealed class DelegatedContext
    {
    }

    public sealed class DelegatedViewContract
    {
    }

    public sealed class DelegatedViewParameters
    {
    }

    [QueryView(
        Name = "delegated-view",
        DisplayName = "Delegated View",
        Description = "Delegated View",
        Version = "1.0.0")]
    public sealed class DelegatedView : IDelegateQueryViewSource<DelegatedContext, DelegatedViewContract, DelegatedViewParameters>
    {
        public Task<QueryResult<DelegatedViewContract>> ExecuteAsync(
            IQueryRequest<DelegatedViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<DelegatedViewContract>(0, 0, 25, []));
    }
}
