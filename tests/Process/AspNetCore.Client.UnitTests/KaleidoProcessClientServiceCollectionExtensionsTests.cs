using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Client.Tests;

public sealed class KaleidoProcessClientServiceCollectionExtensionsTests
{
    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddProcessClient("name", "http://localhost"));
    }

    [Fact]
    public void AddProcessClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessClient("", "http://localhost"));
    }

    [Fact]
    public void AddProcessClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessClient("name", ""));
    }

    // ---------------------------------------------------------------------------
    // Factory registration
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessClient("RemoteProcessor", "http://localhost");

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoProcessClientFactory));
    }

    [Fact]
    public void AddProcessClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddProcessClient("RemoteProcessor", "http://localhost");

        Assert.Same(builder, result);
    }

    // ---------------------------------------------------------------------------
    // RouteOptionsMap
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessClient("Radiology", "http://localhost", routePrefix: "radiology");

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap));
        var map = (KaleidoProcessClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var opts));
        Assert.Equal("radiology", opts!.RoutePrefix);
    }

    [Fact]
    public void AddProcessClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddProcessClient("ProcessorA", "http://a.localhost")
            .AddProcessClient("ProcessorB", "http://b.localhost", routePrefix: "prefix");

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoProcessClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ProcessorA"));
        Assert.True(map.Options.ContainsKey("ProcessorB"));
    }

    [Fact]
    public void AddProcessClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessClient("RemoteProcessor", "http://localhost");

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap));
        var map = (KaleidoProcessClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var opts));
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
