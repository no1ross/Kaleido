using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Tests;

public sealed class QueryableAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryableAspNetCore_WhenBuilderIsNull_Throws()
    {
        IQueryableBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddQueryableAspNetCore());
    }

    [Fact]
    public void AddQueryableAspNetCore_WhenQueryableIsNotRegistered_Throws()
    {
        var builder = new TestQueryableBuilder(new ServiceCollection(), [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddQueryableAspNetCore());

        Assert.Equal("AddQueryable must be called before AddQueryableAspNetCore.", exception.Message);
    }

    [Fact]
    public void AddQueryableAspNetCore_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableService, FakeQueryableService>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var result = builder.AddQueryableAspNetCore();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddQueryableAspNetCore_RegistersConfiguredRouteOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableService, FakeQueryableService>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddQueryableAspNetCore(options =>
        {
            options.RoutePrefix = "/custom";
            options.QueryRoute = "execute";
            options.MetadataRoute = "schema";
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<QueryableRouteOptions>();

        Assert.Equal("/custom", options.RoutePrefix);
        Assert.Equal("execute", options.QueryRoute);
        Assert.Equal("schema", options.MetadataRoute);
    }

    [Fact]
    public void AddQueryableAspNetCore_RegistersRoutingServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryableService, FakeQueryableService>();
        var builder = new TestQueryableBuilder(services, [typeof(QueryableAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddQueryableAspNetCore();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    private sealed class TestQueryableBuilder : IQueryableBuilder
    {
        public TestQueryableBuilder(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }

    private sealed class FakeQueryableService : IQueryableService
    {
        public Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(IQueryRequest request, CancellationToken cancellationToken = default)
            where TQueryView : class
            where TView : class =>
            Task.FromResult(new QueryResult<TView>(0, 0, 0, Array.Empty<TView>()));
    }
}
