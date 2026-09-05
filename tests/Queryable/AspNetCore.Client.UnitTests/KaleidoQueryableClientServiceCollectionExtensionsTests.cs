using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Client.Tests;

public sealed class KaleidoQueryableClientServiceCollectionExtensionsTests
{
    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddQueryableClient("name", "http://localhost"));
    }

    [Fact]
    public void AddQueryableClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient("", "http://localhost"));
    }

    [Fact]
    public void AddQueryableClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient("name", ""));
    }

    // ---------------------------------------------------------------------------
    // Factory registration
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient("MemberService", "http://localhost");

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoQueryableClientFactory));
    }

    [Fact]
    public void AddQueryableClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddQueryableClient("MemberService", "http://localhost");

        Assert.Same(builder, result);
    }

    // ---------------------------------------------------------------------------
    // RouteOptionsMap
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient("Radiology", "http://localhost", routePrefix: "radiology");

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var opts));
        Assert.Equal("radiology", opts!.RoutePrefix);
    }

    [Fact]
    public void AddQueryableClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddQueryableClient("ServiceA", "http://a.localhost")
            .AddQueryableClient("ServiceB", "http://b.localhost", routePrefix: "prefix");

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoQueryableClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ServiceA"));
        Assert.True(map.Options.ContainsKey("ServiceB"));
    }

    [Fact]
    public void AddQueryableClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient("MemberService", "http://localhost");

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("MemberService", out var opts));
        Assert.Equal("", opts!.RoutePrefix);
    }

    // ---------------------------------------------------------------------------
    // Test double
    // ---------------------------------------------------------------------------

    private sealed class FakeKaleidoBuilder : IKaleidoBuilder
    {
        public FakeKaleidoBuilder(IServiceCollection services)
        {
            Services = services;
            Assemblies = [typeof(FakeKaleidoBuilder).Assembly];
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }
}
