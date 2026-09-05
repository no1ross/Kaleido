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
            () => builder!.AddProcessClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenConfigureIsNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddProcessClient(null!));
    }

    [Fact]
    public void AddProcessClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessClient(o =>
            {
                o.Name = "";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "";
            }));
    }

    // ---------------------------------------------------------------------------
    // Factory registration
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoProcessClientFactory));
    }

    [Fact]
    public void AddProcessClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddProcessClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

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

        builder.AddProcessClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RoutePrefix = "radiology";
        });

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
            .AddProcessClient(o => { o.Name = "ProcessorA"; o.BaseUrl = "http://a.localhost"; })
            .AddProcessClient(o => { o.Name = "ProcessorB"; o.BaseUrl = "http://b.localhost"; o.RoutePrefix = "prefix"; });

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

        builder.AddProcessClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap));
        var map = (KaleidoProcessClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var opts));
        Assert.Equal("", opts!.RoutePrefix);
    }

    // ---------------------------------------------------------------------------
    // configureClient callback
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
        // configure runs before configureClient, so RoutePrefix set in configure is
        // stored in the map (configureClient no longer receives the options object).
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
                o.RoutePrefix = "kaleido";
            });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap));
        var map = (KaleidoProcessClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var stored));
        Assert.Equal("kaleido", stored!.RoutePrefix);
    }

    [Fact]
    public void AddProcessClient_WithConfigureClient_InvokesCallbackWithHttpClientBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);
        var callbackInvoked = false;

        builder.AddProcessClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
            },
            http =>
            {
                Assert.NotNull(http);
                callbackInvoked = true;
            });

        Assert.True(callbackInvoked);
    }

    // ---------------------------------------------------------------------------
    // Test doubles
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
