using Kaleido;
using Kaleido.AspNetCore.Queryable;
using Kaleido.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Tests;

public sealed class QueryableAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryableAspNetCore_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddQueryableAspNetCore());
    }

    [Fact]
    public void AddQueryableAspNetCore_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableRegistry, FakeQueryableRegistry>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var result = builder.AddQueryableAspNetCore();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddQueryableAspNetCore_RegistersRoutingAndNoRouteOptions()
    {
        // RouteOptions (prefix, query route, metadata route) are no longer configurable —
        // routes are derived from KaleidoServiceOptions.ServiceName.
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableRegistry, FakeQueryableRegistry>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddQueryableAspNetCore();

        using var provider = services.BuildServiceProvider();

        // Routing infrastructure is registered.
        Assert.NotNull(provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    [Fact]
    public void AddQueryableAspNetCore_RegistersRoutingServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableRegistry, FakeQueryableRegistry>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddQueryableAspNetCore();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    private sealed class TestQueryableBuilder : IKaleidoBuilder
    {
        public TestQueryableBuilder(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
        public IConfiguration Configuration { get; } =
            new ConfigurationBuilder().Build();
        public KaleidoServiceOptions ServiceOptions { get; } =
            new() { ServiceName = "test" };
    }

    private sealed class FakeQueryableRegistry : IQueryableRegistry
    {
        public IReadOnlyCollection<QueryableContextRegistryItem> Registrations => Array.Empty<QueryableContextRegistryItem>();
        public QueryableContextRegistryItem? Find(string name) => null;
        public QueryableContextRegistryItem GetRegistration(string name) => throw new NotImplementedException();
    }
}
